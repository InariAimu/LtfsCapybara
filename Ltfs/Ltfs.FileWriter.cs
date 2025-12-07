using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.IO.Hashing;

using Ltfs.Index;

using TapeDrive;

namespace Ltfs;

public partial class Ltfs
{
    // number of buffers in the ring buffer
    const int BufferCount = 8;

    private Channel<BufferItem>? rawDataChannel = Channel.CreateBounded<BufferItem>(BufferCount);
    private Channel<BufferItem>? crcDataChannel = Channel.CreateBounded<BufferItem>(BufferCount);
    private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;

    private ulong writeBlockPos = 0;

    private sealed class BufferItem
    {
        public required IMemoryOwner<byte> Owner;
        public int Length = 0;
    }

    private readonly Crc64 crc64 = new Crc64();

    public async Task<bool> WriteAll()
    {
        int blocksize = LtfsLabelA.Blocksize;

        // ensure tape driver uses the block size
        _tapeDrive.SetBlockSize((ulong)blocksize);

        writeBlockPos = 0;
        ulong totalWrite = 0;
        ulong totalWriteSinceLastIndex = 0;

        ulong totalBytesToWrite = (ulong)fileTasks
            .Where(t => t.TaskType != FileTaskType.Delete)
            .Sum(t => new FileInfo(t.LocalPath).Length);

        var t = DateTime.Now.Ticks;

        foreach (var task in fileTasks.Where(t => !t.IsTaskDone))
        {
            if (task.TaskType == FileTaskType.Delete)
            {
                UpdateIndexByTask(GetLatestIndex()!, task);
                task.IsTaskDone = true;
                continue;
            }

            FileInfo fi = new FileInfo(task.LocalPath);
            if (!fi.Exists)
            {
                Console.WriteLine($"File not found: {task.LocalPath}");
                task.IsTaskError = true;
                continue;
            }

            try
            {
                await WriteFile(task);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing file {task.LocalPath}: {ex.Message}");
                task.IsTaskError = true;
                continue;
            }

            totalWrite += (ulong)fi.Length;
            totalWriteSinceLastIndex += (ulong)fi.Length;
            task.IsTaskDone = true;

            UpdateIndexByTask(GetLatestIndex()!, task);

            Console.WriteLine($"{totalWrite/1024.0/1024/1024:f2}G / {totalBytesToWrite/1024.0/1024/1024:f2}G written Progress: {totalWrite * 100 / totalBytesToWrite}%");

            if (totalWriteSinceLastIndex >= 30ul * 1024 * 1024 * 1024) // every 30GB
            {
                Console.WriteLine("Writing intermediate index to tape...");
                WriteIndexToDataPartition();
                totalWriteSinceLastIndex = 0;
            }

        }

        Console.WriteLine("All files written.");
        Console.WriteLine($"Total time: {(DateTime.Now.Ticks - t) / 10000000.0:f2} seconds");
        var time = DateTime.Now.Ticks - t;
        if (time == 0) time = 1;
        Console.WriteLine($"Speed: {totalWrite * 10000000.0 / 1024 / 1024 / time:f2} MB/s");

        foreach (var task in fileTasks)
        {
            if (task.IsTaskError)
            {
                Console.WriteLine($"Task error: {task.LocalPath}");
            }
        }

        return true;
    }

    private async Task WriteFile(FileTask task)
    {
        var blockSize = LtfsLabelA.Blocksize;

        using var file = new FileStream(
            task.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: blockSize, useAsync: true);

        var dataPartition = LtfsLabelA.Partitions.Data;

        var pos = _tapeDrive.ReadPosition();
        if (pos.BlockNumber <= writeBlockPos)
        {
            Console.WriteLine("No need to locate");
        }
        else
        {
            // position to end-of-data for append
            Console.WriteLine($"Locating tape");
            _tapeDrive.Locate(0, PartitionToNumber(dataPartition), LocateType.EOD);
            pos = _tapeDrive.ReadPosition();
        }

        ulong startBlock = pos.BlockNumber;

        Console.WriteLine($"Start block: {startBlock}");

        crc64.Reset();

        Console.WriteLine($"Start writing file: {task.LocalPath}");
        Console.WriteLine($"File size: {file.Length / 1024 / 1024} MB");
        var t = DateTime.Now.Ticks;

        rawDataChannel ??= Channel.CreateBounded<BufferItem>(BufferCount);
        crcDataChannel ??= Channel.CreateBounded<BufferItem>(BufferCount);

        // create writer(consumer) task
        var crcTask = Task.Run(() => Crc64Task());
        var writerTask = Task.Run(() => TapeWriterTask());

        int smbBuffersize = 1024 * 1024 * 4; // 4MB for SMB performance

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

        // finish file
        _tapeDrive.WriteFileMark();

        Console.WriteLine($"Total time: {(DateTime.Now.Ticks - t) / 10000000.0:f2} seconds");
        var time = DateTime.Now.Ticks - t;
        if (time == 0) time = 1;
        Console.WriteLine($"Speed: {file.Length * 10000000.0 / 1024 / 1024 / time:f2} MB/s");

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
                    ByteCount = (ulong)file.Length
                }
            ]
        };
        task.LtfsPath.ExtendedAttributes = new ExtendedAttributes()
        {
            Xattrs = [
                new XAttr("ltfs.hash.crc64sum", crc64.GetCurrentHashAsUInt64().ToString("X16")),
            ]
        };
    }

    private async Task Crc64Task()
    {
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
        while (await crcDataChannel.Reader.WaitToReadAsync())
        {
            while (crcDataChannel.Reader.TryRead(out var buf))
            {
                _tapeDrive.WriteMao(buf.Owner.Memory[..buf.Length], LtfsLabelA.Blocksize);
                buf.Owner.Dispose();
            }
        }
    }
}
