using System.Buffers;
using System.Threading.Channels;
using System.IO.Hashing;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Ltfs.Index;
using Ltfs.Utils;
using Ltfs.Tasks;

using TapeDrive;

namespace Ltfs;

public partial class Ltfs
{
    const int DefaultStreamingChunkSize = 1024 * 1024 * 4; // 4MB for SMB performance
    const int DefaultSmallFilePrefetchThreshold = 1024 * 1024 * 4;
    const int DefaultSmallFileDirectWriteThreshold = 128 * 1024;
    const int DefaultSmallFilePrefetchConcurrency = 16;
    const int DefaultSmallFilePrefetchWindow = 32;
    const ulong IndexInterval = 30ul * 1024 * 1024 * 1024; // 30GB

    // number of buffers in the ring buffer
    const int BufferCount = 8;

    private Channel<BufferItem>? rawDataChannel = Channel.CreateBounded<BufferItem>(BufferCount);
    private Channel<BufferItem>? crcDataChannel = Channel.CreateBounded<BufferItem>(BufferCount);
    private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;

    private readonly FileBuffer fileBuffer = new FileBuffer();

    public int SmallFilePrefetchThresholdBytes { get; set; } = DefaultSmallFilePrefetchThreshold;
    public int SmallFileDirectWriteThresholdBytes { get; set; } = DefaultSmallFileDirectWriteThreshold;
    public int SmallFilePrefetchConcurrency { get; set; } = DefaultSmallFilePrefetchConcurrency;
    public int SmallFilePrefetchWindow { get; set; } = DefaultSmallFilePrefetchWindow;
    public FileChecksumAlgorithm ChecksumAlgorithm { get; set; } = FileChecksumAlgorithm.Crc64;

    // index of the file currently being written; used by prefetch controller
    private volatile int currentWriteIndex = 0;

    private ulong writeBlockPos = 0;
    
    // Progress tracking for the currently-writing file (used to provide
    // per-file logging updates). These are updated from writer threads.
    private long currentFileBytesWritten = 0;
    private long currentFileSize = 0;
    private string? currentFilePath = null;
    private long currentFileStartTicks = 0;

    private sealed class BufferItem
    {
        public required IMemoryOwner<byte> Owner;
        public int Length = 0;
    }

    private readonly Crc64 crc64 = new Crc64();
    private IncrementalHash? sha1;

    public Task<bool> PerformWriteTasks()
    {
        return Commit(LtfsTaskQueueType.Write);
    }

    private async Task<bool> PerformWriteTasks(IReadOnlyList<WriteTask> writeTasks, LtfsIndex workingIndex)
    {
        if (writeTasks.Count == 0)
            return true;

        var label = LtfsLabelA ?? throw new InvalidOperationException("LTFS label is not loaded.");
        int blocksize = label.Blocksize;

        Logger.Info($"Starting write of {writeTasks.Count} files...");

        _tapeDrive.SetBlockSize((ulong)blocksize);

        writeBlockPos = 0;
        ulong totalWrite = 0;
        ulong totalWriteSinceLastIndex = 0;

        ulong totalBytesToWrite = (ulong)writeTasks
            .Sum(t => new FileInfo(t.LocalPath).Length);

        var t = DateTime.Now.Ticks;

        var smallFilePrefetchThreshold = Math.Max(0, SmallFilePrefetchThresholdBytes);
        var smallFileDirectWriteThreshold = Math.Max(0, SmallFileDirectWriteThresholdBytes);
        var prefetchConcurrency = Math.Max(0, SmallFilePrefetchConcurrency);
        var prefetchWindow = Math.Max(0, SmallFilePrefetchWindow);

        SemaphoreSlim? prefetchSemaphore = null;
        SemaphoreSlim? prefetchWindowSignal = null;
        Task prefetchTask = Task.CompletedTask;
        CancellationTokenSource? overallMonitorCts = null;
        Task overallMonitor = Task.CompletedTask;

        try
        {
            if (smallFilePrefetchThreshold > 0 && prefetchConcurrency > 0 && prefetchWindow > 0)
            {
                prefetchSemaphore = new SemaphoreSlim(prefetchConcurrency);
                prefetchWindowSignal = new SemaphoreSlim(0);

                Logger.Info($"Starting prefetch task with window size {prefetchWindow}, concurrency limit {prefetchConcurrency}, prefetch threshold {FileSize.FormatSize((ulong)smallFilePrefetchThreshold)}, direct-write threshold {FileSize.FormatSize((ulong)smallFileDirectWriteThreshold)}");
                prefetchTask = PrefetchTask(writeTasks, prefetchSemaphore, prefetchWindowSignal, prefetchWindow, smallFilePrefetchThreshold, smallFileDirectWriteThreshold);
            }

            Logger.Info($"Locating tape");
            var dataPartition = label.Partitions.Data.ToString();
            _tapeDrive.Locate(0, PartitionToNumber(dataPartition), LocateType.EOD);

            overallMonitorCts = new CancellationTokenSource();
            var overallMonitorToken = overallMonitorCts.Token;
            var completedItems = 0;
            overallMonitor = Task.Run(() => RunProgressMonitor(
                    LtfsTaskQueueType.Write,
                    writeTasks.Count,
                    () => Volatile.Read(ref completedItems),
                    () => totalWrite + (ulong)Math.Max(0, Interlocked.Read(ref currentFileBytesWritten)),
                    totalBytesToWrite,
                    () => currentFilePath,
                    () => Interlocked.Read(ref currentFileBytesWritten),
                    () => Interlocked.Read(ref currentFileSize),
                    overallMonitorToken),
                overallMonitorToken);

            var hasErrors = false;

            for (int i = 0; i < writeTasks.Count; i++)
            {
                currentWriteIndex = i;

                try { prefetchWindowSignal?.Release(); } catch { }

                var task = writeTasks[i];
                MarkTaskRunning(task);

                try
                {
                    await WriteFile(task);
                }
                catch (TapeDriveCommandException)
                {
                    MarkTaskCancelled(task);
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error writing file {task.LocalPath}: {ex.Message}");
                    MarkTaskFailed(task);
                    hasErrors = true;
                    continue;
                }

                totalWrite += task.LtfsTargetPath.Length;
                totalWriteSinceLastIndex += task.LtfsTargetPath.Length;
                MarkTaskCompleted(task);
                Interlocked.Increment(ref completedItems);

                UpdateIndexByTask(workingIndex, task);

                Logger.Debug($"{FileSize.FormatSize(totalWrite)} / {FileSize.FormatSize(totalBytesToWrite)} written Progress: {totalWrite * 100 / totalBytesToWrite}% Estimated time left: {(DateTime.Now.Ticks - t) / (double)totalWrite * (totalBytesToWrite - totalWrite) / 10000000.0:f2} seconds");

                if (totalWriteSinceLastIndex >= IndexInterval)
                {
                    Logger.Info("Writing intermediate index to tape...");
                    _tapeDrive.WriteFileMark();
                    WriteIndexToDataPartition();
                    totalWriteSinceLastIndex = 0;
                }
            }

            Logger.Info($"All {writeTasks.Count} files written.");
            Logger.Info($"Total time: {(DateTime.Now.Ticks - t) / 10000000.0:f2} seconds");
            var time = DateTime.Now.Ticks - t;
            if (time == 0)
                time = 1;

            Logger.Info($"Average Speed: {FileSize.FormatSize((ulong)(totalWrite * 10000000.0 / time))}/s");

            foreach (var task in writeTasks)
            {
                if (task.IsTaskError)
                {
                    Logger.Error($"Task error: {task.LocalPath}");
                }
            }

            _tapeDrive.WriteFileMark();

            return !hasErrors;
        }
        finally
        {
            overallMonitorCts?.Cancel();
            try { await prefetchTask; } catch { }
            try { await overallMonitor; } catch { }
            try { prefetchWindowSignal?.Dispose(); } catch { }
            try { prefetchSemaphore?.Dispose(); } catch { }
            overallMonitorCts?.Dispose();

            long lastTicks = DateTime.UtcNow.Ticks;
            ulong lastProcessed = totalWrite;
            PublishProgress(BuildProgressSnapshot(
                LtfsTaskQueueType.Write,
                writeTasks.Count,
                writeTasks.Count(task => task.Status is TaskExecutionStatus.Completed or TaskExecutionStatus.Committed),
                totalWrite,
                totalBytesToWrite,
                null,
                0,
                0,
                _lastTapePerformanceSnapshot,
                lastTicks,
                ref lastTicks,
                ref lastProcessed,
                isCompleted: true));
        }
    }

    private async Task WriteFile(WriteTask task)
    {
        var label = LtfsLabelA ?? throw new InvalidOperationException("LTFS label is not loaded.");
        var blockSize = label.Blocksize;

        var dataPartition = label.Partitions.Data.ToString();

        var pos = _tapeDrive.ReadPosition();
        if (pos.BlockNumber <= writeBlockPos)
        {
            //Logger.Trace($"No need to locate tape");
        }
        else
        {
            // position to end-of-data for append
            //Logger.Info($"Locating tape");
            //_tapeDrive.Locate(0, PartitionToNumber(dataPartition), LocateType.EOD);
            //pos = _tapeDrive.ReadPosition();
        }

        ulong startBlock = pos.BlockNumber;
        Logger.Debug($"Start block: {startBlock}");

        ResetHashes();

        long fileLength = (long)task.LtfsTargetPath.Length;
        if (fileLength == 0)
        {
            Logger.Warn($"Zero-length file: {task.LocalPath}");
            task.LtfsTargetPath.ExtentInfo = new ExtentInfo
            {
                Extent = [
                    new Extent
                    {
                        Partition = dataPartition,
                        StartBlock = startBlock,
                        FileOffset = 0,
                        ByteOffset = 0,
                        ByteCount = 0
                    }
                ]
            };
            return;
        }

        // Initialize per-file progress tracking
        Interlocked.Exchange(ref currentFileBytesWritten, 0);
        Interlocked.Exchange(ref currentFileSize, fileLength);
        currentFilePath = task.LocalPath;
        Interlocked.Exchange(ref currentFileStartTicks, DateTime.Now.Ticks);

        Logger.Debug($"[Write] {FileSize.FormatSize((ulong)fileLength)} {task.LocalPath}");
        var t = DateTime.Now.Ticks;

        var smallFileDirectWriteThreshold = Math.Max(0, SmallFileDirectWriteThresholdBytes);
        var streamingChunkSize = GetStreamingChunkSize(blockSize);

        // If the file has been prefetched into the FileBuffer, consume from it.
        var bufferedReader = fileBuffer.GetReader(task.LocalPath);
        if (bufferedReader != null)
        {
            Logger.Debug($"[Prefetched buffer]");

            while (await bufferedReader.WaitToReadAsync())
            {
                while (bufferedReader.TryRead(out var buf))
                {
                    AppendHashes(buf.Owner.Memory.Span[..buf.Length]);
                    _tapeDrive.BufferedWrite(buf.Owner.Memory[..buf.Length], LtfsLabelA!.Blocksize);
                    try { Interlocked.Add(ref currentFileBytesWritten, buf.Length); } catch { }
                    buf.Owner.Dispose();
                }
            }
            // Remove the buffer entry and ensure any leftover memory is disposed
            try { await fileBuffer.RemoveAsync(task.LocalPath); } catch { }
        }
        else if (fileLength <= smallFileDirectWriteThreshold)
        {
            Logger.Debug($"[Direct small-file write]");
            await DirectWriteSmallFile(task.LocalPath, blockSize, fileLength);
        }
        else
        {
            rawDataChannel ??= Channel.CreateBounded<BufferItem>(BufferCount);
            crcDataChannel ??= Channel.CreateBounded<BufferItem>(BufferCount);

            // create writer(consumer) task
            var crcTask = Task.Run(() => Crc64Task());
            var writerTask = Task.Run(() => TapeWriterTask());

            using var file = new FileStream(
                task.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: blockSize, useAsync: true);

            while (true)
            {
                var memoryOwner = memoryPool.Rent(streamingChunkSize);
                int read = await file.ReadAsync(memoryOwner.Memory[..streamingChunkSize]);

                if (read == 0)
                {
                    // end of file
                    memoryOwner.Dispose();
                    break;
                }

                await rawDataChannel.Writer.WriteAsync(new BufferItem()
                {
                    Owner = memoryOwner,
                    Length = read,
                });
            }

            rawDataChannel.Writer.Complete();

            await Task.WhenAll(crcTask, writerTask);

            rawDataChannel = null;
            crcDataChannel = null;
        }

        var time = DateTime.Now.Ticks - t;
        if (time == 0) time = 1;
        Logger.Debug($"Total time: {time / 10000000.0:f2} seconds");
        Logger.Debug($"Speed: {FileSize.FormatSize((ulong)(fileLength * 10000000.0 / time))} /s");
        

        pos = _tapeDrive.ReadPosition();
        writeBlockPos = pos.BlockNumber;

        // record a single extent that covers the file written
        task.LtfsTargetPath.ExtentInfo = new ExtentInfo
        {
            Extent = [
                new Extent
                {
                    Partition = dataPartition,
                    StartBlock = startBlock,
                    FileOffset = 0,
                    ByteOffset = 0,
                    ByteCount = (ulong)fileLength
                }
            ]
        };
        task.LtfsTargetPath.ExtendedAttributes = new ExtendedAttributes()
        {
            Xattrs = BuildHashXattrs()
        };

        // Clear per-file progress tracking
        try { currentFilePath = null; } catch { }
        try { Interlocked.Exchange(ref currentFileSize, 0); } catch { }
        try { Interlocked.Exchange(ref currentFileBytesWritten, 0); } catch { }
        try { Interlocked.Exchange(ref currentFileStartTicks, 0); } catch { }
        DisposeHashes();
    }

    private async Task Crc64Task()
    {
        if (rawDataChannel == null || crcDataChannel == null)
            throw new InvalidOperationException("Channels not initialized");

        while (await rawDataChannel.Reader.WaitToReadAsync())
        {
            while (rawDataChannel.Reader.TryRead(out var buf))
            {
                AppendHashes(buf.Owner.Memory.Span[..buf.Length]);
                await crcDataChannel.Writer.WriteAsync(buf);
            }
        }
        crcDataChannel.Writer.Complete();
    }

    private async Task TapeWriterTask()
    {
        if (crcDataChannel == null)
            throw new InvalidOperationException("Channels not initialized");

        while (await crcDataChannel.Reader.WaitToReadAsync())
        {
            while (crcDataChannel.Reader.TryRead(out var buf))
            {
                _tapeDrive.BufferedWrite(buf.Owner.Memory[..buf.Length], LtfsLabelA!.Blocksize);
                try { Interlocked.Add(ref currentFileBytesWritten, buf.Length); } catch { }
                buf.Owner.Dispose();
            }
        }
    }

    private async Task DirectWriteSmallFile(string localPath, int blockSize, long fileLength)
    {
        var chunkSize = GetDirectWriteChunkSize(blockSize, fileLength);

        using var file = new FileStream(
            localPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: chunkSize, useAsync: true);

        while (true)
        {
            var memoryOwner = memoryPool.Rent(chunkSize);
            int read;
            try
            {
                read = await file.ReadAsync(memoryOwner.Memory[..chunkSize]);
            }
            catch
            {
                memoryOwner.Dispose();
                throw;
            }

            if (read == 0)
            {
                memoryOwner.Dispose();
                break;
            }

            try
            {
                AppendHashes(memoryOwner.Memory.Span[..read]);
                _tapeDrive.BufferedWrite(memoryOwner.Memory[..read], blockSize);
                Interlocked.Add(ref currentFileBytesWritten, read);
            }
            finally
            {
                memoryOwner.Dispose();
            }
        }
    }

    private Task PrefetchTask(IReadOnlyList<WriteTask> writeTasks, SemaphoreSlim prefetchSemaphore, SemaphoreSlim prefetchWindowSignal, int prefetchWindow, int smallFilePrefetchThreshold, int smallFileDirectWriteThreshold)
    {
        return Task.Run(async () =>
        {
            int nextToPrefetch = 0;
            while (nextToPrefetch < writeTasks.Count)
            {
                // Only schedule prefetches that fall within the sliding window
                if (nextToPrefetch <= currentWriteIndex + (prefetchWindow - 1))
                {
                    var ptask = writeTasks[nextToPrefetch];
                    if (!string.IsNullOrWhiteSpace(ptask.LocalPath))
                    {
                        try
                        {
                            var filesize = ptask.LtfsTargetPath.Length;
                            if (filesize > (ulong)smallFileDirectWriteThreshold && filesize <= (ulong)smallFilePrefetchThreshold)
                            {
                                // Start producer; it will wait on semaphore before reading.
                                Logger.Debug($"[Prefetch] {FileSize.FormatSize(filesize)} {ptask.LocalPath}");
                                var chunkSize = GetPrefetchChunkSize(filesize, smallFilePrefetchThreshold);
                                _ = fileBuffer.AddFileAsync(ptask.LocalPath, chunkSize, (long)filesize, prefetchSemaphore);
                            }
                        }
                        catch
                        {
                            // ignore prefetch errors
                            Logger.Error($"Prefetch error: {ptask.LocalPath}");
                        }
                    }
                    nextToPrefetch++;
                    continue;
                }

                // Wait until the write loop advances the window (avoids busy-loop)
                await prefetchWindowSignal.WaitAsync();
            }
        });
    }

    private static int GetStreamingChunkSize(int blockSize)
    {
        return Math.Max(blockSize, DefaultStreamingChunkSize);
    }

    private int GetDirectWriteChunkSize(int blockSize, long fileLength)
    {
        var directWriteThreshold = Math.Max(1, SmallFileDirectWriteThresholdBytes);
        var fileSizedChunk = fileLength > 0 ? (int)Math.Min(fileLength, int.MaxValue) : 1;
        return Math.Max(blockSize, Math.Min(directWriteThreshold, fileSizedChunk));
    }

    private static int GetPrefetchChunkSize(ulong fileSize, int smallFilePrefetchThreshold)
    {
        var threshold = Math.Max(1, smallFilePrefetchThreshold);
        var fileSizedChunk = fileSize > 0 ? (int)Math.Min(fileSize, int.MaxValue) : 1;
        return Math.Max(1, Math.Min(threshold, fileSizedChunk));
    }

    private void ResetHashes()
    {
        DisposeHashes();

        if (ShouldComputeCrc64())
        {
            crc64.Reset();
        }

        if (ShouldComputeSha1())
        {
            sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        }
    }

    private void AppendHashes(ReadOnlySpan<byte> data)
    {
        if (ShouldComputeCrc64())
        {
            crc64.Append(data);
        }

        sha1?.AppendData(data);
    }

    private void AppendHashBytes(byte[] data)
    {
        AppendHashes(data);
    }

    private XAttr[] BuildHashXattrs()
    {
        var xattrs = new List<XAttr>(2);

        if (ShouldComputeCrc64())
        {
            xattrs.Add(new XAttr("ltfs.hash.crc64sum", crc64.GetCurrentHashAsUInt64().ToString("X16")));
        }

        if (sha1 != null)
        {
            xattrs.Add(new XAttr("ltfs.hash.sha1sum", Convert.ToHexString(sha1.GetHashAndReset())));
        }

        return xattrs.ToArray();
    }

    private bool ShouldComputeCrc64()
    {
        return ChecksumAlgorithm is FileChecksumAlgorithm.Crc64 or FileChecksumAlgorithm.Crc64AndSha1;
    }

    private bool ShouldComputeSha1()
    {
        return ChecksumAlgorithm is FileChecksumAlgorithm.Sha1 or FileChecksumAlgorithm.Crc64AndSha1;
    }

    private void DisposeHashes()
    {
        sha1?.Dispose();
        sha1 = null;
    }

}
