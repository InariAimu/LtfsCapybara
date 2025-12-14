using System.Buffers;
using System.Threading.Channels;
using System.IO.Hashing;
using System.Threading;
using System.Threading.Tasks;

using Ltfs.Index;
using Ltfs.Utils;

using TapeDrive;

namespace Ltfs;

public partial class Ltfs
{
    const int ChannelThreshold = 1024 * 1024 * 4; // 4MB for SMB performance
    const ulong IndexInterval = 30ul * 1024 * 1024 * 1024; // 30GB

    // number of buffers in the ring buffer
    const int BufferCount = 8;

    private Channel<BufferItem>? rawDataChannel = Channel.CreateBounded<BufferItem>(BufferCount);
    private Channel<BufferItem>? crcDataChannel = Channel.CreateBounded<BufferItem>(BufferCount);
    private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;

    private readonly FileBuffer fileBuffer = new FileBuffer();

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

    public async Task<bool> PerformWriteTasks()
    {
        int blocksize = LtfsLabelA.Blocksize;

        // do delete tasks first and remove from task list
        writeTasks.RemoveAll(t =>
        {
            if (t.TaskType == FileTaskType.Delete)
            {
                UpdateIndexByTask(GetLatestIndex()!, t);
                t.IsTaskDone = true;
                return true;
            }
            return false;
        });

        Logger.Info($"Starting write of {writeTasks.Count} files...");

        // ensure tape driver uses the block size
        _tapeDrive.SetBlockSize((ulong)blocksize);

        writeBlockPos = 0;
        ulong totalWrite = 0;
        ulong totalWriteSinceLastIndex = 0;

        ulong totalBytesToWrite = (ulong)writeTasks
            .Sum(t => new FileInfo(t.LocalPath).Length);

        var t = DateTime.Now.Ticks;

        // Start a background prefetch controller that keeps a sliding window
        // of prefetched files ahead of the current write index. Concurrency
        // of actual file-read producers is limited by `prefetchSemaphore`.
        var prefetchSemaphore = new SemaphoreSlim(8);
        const int prefetchWindow = 32;

        // Signal used to wake the prefetcher when the write index advances.
        var prefetchWindowSignal = new SemaphoreSlim(0);

        // Run prefetch task before locating tape to avoid delays.
        Logger.Info($"Starting prefetch task with window size {prefetchWindow} and concurrency limit {prefetchSemaphore.CurrentCount}");
        var prefetchTask = PrefetchTask(prefetchSemaphore, prefetchWindowSignal, prefetchWindow);

        // position to end-of-data for append
        Logger.Info($"Locating tape");
        var dataPartition = LtfsLabelA.Partitions.Data;
        _tapeDrive.Locate(0, PartitionToNumber(dataPartition), LocateType.EOD);


        // Start a single monitor for the whole WriteAll operation. The
        // implementation is extracted into `RunOverallMonitor` below.
        using var overallMonitorCts = new CancellationTokenSource();
        var overallMonitorToken = overallMonitorCts.Token;
        var overallMonitor = Task.Run(() =>
                RunOverallMonitor(() => totalWrite, totalBytesToWrite, overallMonitorToken),
            overallMonitorToken);

        for (int i = 0; i < writeTasks.Count; i++)
        {
            currentWriteIndex = i;

            // Notify the prefetch controller that the write index advanced
            try { prefetchWindowSignal.Release(); } catch { }
            var task = writeTasks[i];

            try
            {
                await WriteFile(task);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error writing file {task.LocalPath}: {ex.Message}");
                task.IsTaskError = true;
                continue;
            }

            totalWrite += task.LtfsPath.Length;
            totalWriteSinceLastIndex += task.LtfsPath.Length;
            task.IsTaskDone = true;

            UpdateIndexByTask(GetLatestIndex()!, task);

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
        if (time == 0) time = 1;
        Logger.Info($"Average Speed: {FileSize.FormatSize((ulong)(totalWrite * 10000000.0 / time))}/s");


        foreach (var task in writeTasks)
        {
            if (task.IsTaskError)
            {
                Logger.Error($"Task error: {task.LocalPath}");
            }
        }

        // Wait for the prefetch task to finish scheduling (it will exit when
        // it has processed all entries in `writeTasks`). Then dispose the
        // signal used to wake it.
        try { await prefetchTask; } catch { }
        try { prefetchWindowSignal.Dispose(); } catch { }

        overallMonitorCts.Cancel();
        try { await overallMonitor; } catch { }

        // finish file writes
        _tapeDrive.WriteFileMark();

        return true;
    }

    private async Task WriteFile(WriteTask task)
    {
        var blockSize = LtfsLabelA.Blocksize;

        var dataPartition = LtfsLabelA.Partitions.Data;

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

        crc64.Reset();

        long fileLength = (long)task.LtfsPath.Length;
        if (fileLength == 0)
        {
            Logger.Warn($"Zero-length file: {task.LocalPath}");
            task.LtfsPath.ExtentInfo = new ExtentInfo
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

        int smbBuffersize = ChannelThreshold;

        // If the file has been prefetched into the FileBuffer, consume from it.
        var bufferedReader = fileBuffer.GetReader(task.LocalPath);
        if (bufferedReader != null)
        {
            Logger.Debug($"[Prefetched buffer]");

            while (await bufferedReader.WaitToReadAsync())
            {
                while (bufferedReader.TryRead(out var buf))
                {
                    crc64.Append(buf.Owner.Memory.Span[..buf.Length]);
                    _tapeDrive.BufferedWrite(buf.Owner.Memory[..buf.Length], LtfsLabelA.Blocksize);
                    try { Interlocked.Add(ref currentFileBytesWritten, buf.Length); } catch { }
                    buf.Owner.Dispose();
                }
            }
            // Remove the buffer entry and ensure any leftover memory is disposed
            try { await fileBuffer.RemoveAsync(task.LocalPath); } catch { }
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
                var memoryOwner = memoryPool.Rent(smbBuffersize);
                int read = await file.ReadAsync(memoryOwner.Memory[..smbBuffersize]);

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
        task.LtfsPath.ExtentInfo = new ExtentInfo
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
        task.LtfsPath.ExtendedAttributes = new ExtendedAttributes()
        {
            Xattrs = [
                new XAttr("ltfs.hash.crc64sum", crc64.GetCurrentHashAsUInt64().ToString("X16")),
            ]
        };

        // Clear per-file progress tracking
        try { currentFilePath = null; } catch { }
        try { Interlocked.Exchange(ref currentFileSize, 0); } catch { }
        try { Interlocked.Exchange(ref currentFileBytesWritten, 0); } catch { }
        try { Interlocked.Exchange(ref currentFileStartTicks, 0); } catch { }
    }

    private async Task Crc64Task()
    {
        if (rawDataChannel == null || crcDataChannel == null)
            throw new InvalidOperationException("Channels not initialized");

        while (await rawDataChannel.Reader.WaitToReadAsync())
        {
            while (rawDataChannel.Reader.TryRead(out var buf))
            {
                crc64.Append(buf.Owner.Memory.Span[..buf.Length]);
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
                _tapeDrive.BufferedWrite(buf.Owner.Memory[..buf.Length], LtfsLabelA.Blocksize);
                try { Interlocked.Add(ref currentFileBytesWritten, buf.Length); } catch { }
                buf.Owner.Dispose();
            }
        }
    }

    private Task PrefetchTask(SemaphoreSlim prefetchSemaphore, SemaphoreSlim prefetchWindowSignal, int prefetchWindow)
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
                    if (ptask.TaskType != FileTaskType.Delete)
                    {
                        try
                        {
                            var filesize = ptask.LtfsPath.Length;
                            if (filesize <= ChannelThreshold)
                            {
                                // Start producer; it will wait on semaphore before reading.
                                Logger.Debug($"[Prefetch] {FileSize.FormatSize(filesize)} {ptask.LocalPath}");
                                _ = fileBuffer.AddFileAsync(ptask.LocalPath, ChannelThreshold, prefetchSemaphore);
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

    // Runs an overall monitor for the WriteAll operation. `getTotalWritten`
    // is invoked to obtain the total bytes written so far (outside the
    // monitor) so the monitor can display overall progress.
    private async Task RunOverallMonitor(Func<ulong> getTotalWritten, ulong totalBytesToWrite, CancellationToken token)
    {
        var startTicks = DateTime.Now.Ticks;
        long lastTicks = startTicks;
        // include any in-progress bytes initially
        long currentWrittenInit = Interlocked.Read(ref currentFileBytesWritten);
        ulong lastOverallWritten = getTotalWritten() + (ulong)Math.Max(0, currentWrittenInit);

        try
        {
            while (!token.IsCancellationRequested)
            {
                // Compute overall bytes written including any in-progress file bytes
                long currentWritten = Interlocked.Read(ref currentFileBytesWritten);
                ulong overallWritten = getTotalWritten() + (ulong)Math.Max(0, currentWritten);

                var nowTicks = DateTime.Now.Ticks;
                var elapsedTicks = nowTicks - startTicks;
                if (elapsedTicks <= 0) elapsedTicks = 1;
                double elapsedSecTotal = elapsedTicks / 10000000.0;

                // Compute instantaneous speed over the last interval (approx 1s)
                var intervalTicks = nowTicks - lastTicks;
                if (intervalTicks <= 0) intervalTicks = 1;
                double intervalSec = intervalTicks / 10000000.0;
                ulong delta = overallWritten >= lastOverallWritten ? overallWritten - lastOverallWritten : 0ul;
                double speedInstant = delta / Math.Max(1e-6, intervalSec); // bytes per second over last interval

                // Keep overall average speed for ETA calculation
                double speedOverall = overallWritten / Math.Max(1.0, elapsedSecTotal); // bytes per second (average)

                double percentOverall = totalBytesToWrite > 0 ? (overallWritten * 100.0 / totalBytesToWrite) : 0.0;
                double etaOverall = speedOverall > 0 ? (totalBytesToWrite > overallWritten ? (totalBytesToWrite - overallWritten) / speedOverall : 0.0) : double.PositiveInfinity;

                var statusOverall = $"Total: {FileSize.FormatSize(overallWritten)} / {FileSize.FormatSize(totalBytesToWrite)} {percentOverall:f1}% ETA: {etaOverall:f0}s Speed: {FileSize.FormatSize((ulong)speedInstant)}/s";

                if (Log.Current is ConsoleLogger cl)
                {
                    try
                    {
                        lock (Console.Out)
                        {
                            int bottom = Math.Max(0, Console.WindowHeight - 1);
                            try { Console.SetCursorPosition(0, bottom); } catch { }
                            try
                            {
                                Console.SetCursorPosition(0, bottom);
                                cl.WriteLevelPrefix(LogLevel.Info);
                                var toWrite = statusOverall.PadRight(Math.Max(0, Console.WindowWidth - 4));
                                Console.Write(toWrite);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
                else
                {
                    Logger.Info(statusOverall);
                }

                // Update last-sample trackers and wait ~1s
                lastOverallWritten = overallWritten;
                lastTicks = nowTicks;

                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }
}
