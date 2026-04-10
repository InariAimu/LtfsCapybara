using System;
using System.IO.Hashing;
using System.Reflection;
using System.Security.Cryptography;
using System.Reflection.Emit;
using System.Xml.Schema;
using System.Xml;
using System.Xml.Serialization;
using Xunit.Abstractions;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Ltfs.Label;
using Ltfs;
using Ltfs.Index;
using Microsoft.Extensions.Logging.Abstractions;
using LtfsServer.Features.LocalTapes;
using Ltfs.Tasks;
using TapeDrive;

namespace LtfsTest;

public class LtfsTest
{
    [Fact]
    public void Vol1Test()
    {
        var vol1 = new Vol1Label("TEST01L6", "");
        try
        {
            Vol1Label.ToByteArray(vol1);
        }
        catch (Exception ex)
        {
            Assert.Fail(ex.Message);
        }
    }


    [Fact]
    public void FindFile()
    {
        Ltfs.Ltfs ltfs = new Ltfs.Ltfs();
        ltfs.LtfsDataTempIndexs.Clear();
        ltfs.LtfsDataTempIndexs.Add(LtfsIndex.Default());

        var index = ltfs.GetLatestIndex();

        var dir1 = LtfsDirectory.Default();
        dir1.Name = "dir1";
        index.Root["dir1"] = dir1;

        var file1 = LtfsFile.Default();
        file1.Name = "file1.txt";
        dir1["file1.txt"] = file1;

        var findFile1 = ltfs.FindFile("/dir1/file1.txt") as LtfsFile;

        Assert.NotNull(findFile1);
        Assert.True(findFile1.Name.Value == "file1.txt");

    }


    [Fact]
    public void AddFile()
    {
        Ltfs.Ltfs ltfs = new Ltfs.Ltfs();
        ltfs.LtfsDataTempIndexs.Clear();
        ltfs.LtfsDataTempIndexs.Add(LtfsIndex.Default());

        ltfs.CreateDirectory("/dir1");

        var dir = ltfs.FindDirectory("/dir1");

        Assert.NotNull(dir);
        Assert.Equal("dir1", dir!.Name.Value);
    }


    [Fact]
    public void AddFile_ExistingFile_QueuesDeleteThenWrite()
    {
        Ltfs.Ltfs ltfs = new Ltfs.Ltfs();
        ltfs.LtfsDataTempIndexs.Clear();
        ltfs.LtfsDataTempIndexs.Add(LtfsIndex.Default());

        var index = ltfs.GetLatestIndex();
        var original = LtfsFile.Default();
        original.Name = "file1.txt";
        index.Directory["file1.txt"] = original;

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "replacement");

            ltfs.AddFile(tempFile, "/file1.txt");

            var tasks = ltfs.GetPendingTasks();
            Assert.Equal(2, tasks.Count);
            Assert.IsType<DeleteTask>(tasks[0]);
            Assert.IsType<WriteTask>(tasks[1]);
            Assert.Equal("/file1.txt", ((DeleteTask)tasks[0]).TargetPath);
            Assert.Equal("/file1.txt", ((WriteTask)tasks[1]).TargetPath);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }


    [Fact]
    public void DeletePath_RemovesPendingWriteForNewFile()
    {
        Ltfs.Ltfs ltfs = new Ltfs.Ltfs();
        ltfs.LtfsDataTempIndexs.Clear();
        ltfs.LtfsDataTempIndexs.Add(LtfsIndex.Default());

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "new file");

            ltfs.AddFile(tempFile, "/new-file.txt");
            ltfs.DeletePath("/new-file.txt");

            Assert.Empty(ltfs.GetPendingTasks());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }


    [Fact]
    public void PendingTasks_AreSplitIntoReadAndWriteQueues()
    {
        var ltfs = CreateReadReadyLtfs(new ScriptedReadTapeDrive(), blockSize: 4);
        var sourceData = "DATA"u8.ToArray();
        AddLtfsReadSource(ltfs, "/from-tape.bin", 12, sourceData);

        var tempFile = Path.GetTempFileName();
        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(tempFile, "write-side");

            ltfs.AddFile(tempFile, "/write-side.bin");
            ltfs.AddReadTask("/from-tape.bin", Path.Combine(tempDirectory.FullName, "from-tape.bin"));

            var writeTasks = ltfs.GetPendingWriteTasks();
            var readTasks = ltfs.GetPendingReadTasks();
            var allTasks = ltfs.GetPendingTasks();

            Assert.Single(writeTasks);
            Assert.IsType<WriteTask>(writeTasks[0]);
            Assert.Single(readTasks);
            Assert.IsType<ReadTask>(readTasks[0]);
            Assert.Equal(2, allTasks.Count);
        }
        finally
        {
            File.Delete(tempFile);
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void AddReadTask_WhenSourceIsDirectory_ExpandsFilesAndRewritesTargets()
    {
        var ltfs = CreateReadReadyLtfs(new ScriptedReadTapeDrive(), blockSize: 4);
        AddLtfsReadSource(ltfs, "/media/root.bin", 10, "ROOT"u8.ToArray());
        AddLtfsReadSource(ltfs, "/media/sub/leaf.bin", 11, "LEAF"u8.ToArray());

        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            ltfs.AddReadTask("/media", tempDirectory.FullName);

            var readTasks = ltfs.GetPendingReadTasks().OfType<ReadTask>().OrderBy(task => task.SourcePath).ToArray();

            Assert.Equal(2, readTasks.Length);
            Assert.Equal("/media/root.bin", readTasks[0].SourcePath);
            Assert.Equal(Path.Combine(tempDirectory.FullName, "root.bin"), readTasks[0].TargetPath);
            Assert.Equal("/media/sub/leaf.bin", readTasks[1].SourcePath);
            Assert.Equal(Path.Combine(tempDirectory.FullName, "sub", "leaf.bin"), readTasks[1].TargetPath);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void AddVerifyTask_WhenSourceIsDirectory_ExpandsFilesRecursively()
    {
        var ltfs = CreateReadReadyLtfs(new ScriptedReadTapeDrive(), blockSize: 4);
        AddLtfsReadSource(ltfs, "/verify/root.bin", 10, "ROOT"u8.ToArray());
        AddLtfsReadSource(ltfs, "/verify/sub/leaf.bin", 11, "LEAF"u8.ToArray());

        ltfs.AddVerifyTask("/verify");

        var verifyTasks = ltfs.GetPendingVerifyTasks().OfType<VerifyTask>().OrderBy(task => task.SourcePath).ToArray();

        Assert.Equal(2, verifyTasks.Length);
        Assert.Equal("/verify/root.bin", verifyTasks[0].SourcePath);
        Assert.Equal("/verify/sub/leaf.bin", verifyTasks[1].SourcePath);
    }


    [Fact]
    public void RemoveFile()
    {

    }


    [Fact]
    public void RemoveAll()
    {

    }


    [Fact]
    public void AddDirectory()
    {

    }


    [Fact]
    public Task LocalTapeRegistry_FindsLatestCmEntry()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PrefetchTask_SkipsFilesBelowDirectWriteThreshold()
    {
        var ltfs = new Ltfs.Ltfs
        {
            SmallFileDirectWriteThresholdBytes = 4 * 1024,
            SmallFilePrefetchThresholdBytes = 64 * 1024,
        };

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, new byte[1024]);

            var ltfsFile = LtfsFile.Default();
            ltfsFile.Name = "tiny.bin";
            ltfsFile.Length = 1024;

            var writeTask = new WriteTask
            {
                LocalPath = tempFile,
                TargetPath = "/tiny.bin",
                LtfsTargetPath = ltfsFile,
            };

            using var prefetchSemaphore = new SemaphoreSlim(1);
            using var prefetchSignal = new SemaphoreSlim(0);

            await InvokePrefetchTaskAsync(ltfs, [writeTask], prefetchSemaphore, prefetchSignal, 8, ltfs.SmallFilePrefetchThresholdBytes, ltfs.SmallFileDirectWriteThresholdBytes);

            var reader = GetBufferedReader(ltfs, tempFile);
            Assert.Null(reader);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PrefetchTask_BuffersFilesAboveDirectWriteThreshold()
    {
        var ltfs = new Ltfs.Ltfs
        {
            SmallFileDirectWriteThresholdBytes = 4 * 1024,
            SmallFilePrefetchThresholdBytes = 64 * 1024,
        };

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, new byte[8 * 1024]);

            var ltfsFile = LtfsFile.Default();
            ltfsFile.Name = "prefetch.bin";
            ltfsFile.Length = 8 * 1024;

            var writeTask = new WriteTask
            {
                LocalPath = tempFile,
                TargetPath = "/prefetch.bin",
                LtfsTargetPath = ltfsFile,
            };

            using var prefetchSemaphore = new SemaphoreSlim(1);
            using var prefetchSignal = new SemaphoreSlim(0);

            await InvokePrefetchTaskAsync(ltfs, [writeTask], prefetchSemaphore, prefetchSignal, 8, ltfs.SmallFilePrefetchThresholdBytes, ltfs.SmallFileDirectWriteThresholdBytes);

            var reader = GetBufferedReader(ltfs, tempFile);
            Assert.NotNull(reader);

            await RemoveBufferedFileAsync(ltfs, tempFile);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void BuildHashXattrs_UsesCrc64WhenConfigured()
    {
        var xattrs = BuildHashXattrs(FileChecksumAlgorithm.Crc64, "abc"u8.ToArray());

        Assert.Single(xattrs);
        Assert.Equal("ltfs.hash.crc64sum", xattrs[0].Key.Value);
        Assert.Equal("66501A349A0E0855", xattrs[0].Value.Value);
    }

    [Fact]
    public void BuildHashXattrs_UsesSha1WhenConfigured()
    {
        var xattrs = BuildHashXattrs(FileChecksumAlgorithm.Sha1, "abc"u8.ToArray());

        Assert.Single(xattrs);
        Assert.Equal("ltfs.hash.sha1sum", xattrs[0].Key.Value);
        Assert.Equal(Convert.ToHexString(SHA1.HashData("abc"u8.ToArray())), xattrs[0].Value.Value);
    }

    [Fact]
    public void BuildHashXattrs_UsesBothHashesWhenConfigured()
    {
        var xattrs = BuildHashXattrs(FileChecksumAlgorithm.Crc64AndSha1, "abc"u8.ToArray());

        Assert.Equal(2, xattrs.Length);
        Assert.Equal("ltfs.hash.crc64sum", xattrs[0].Key.Value);
        Assert.Equal("66501A349A0E0855", xattrs[0].Value.Value);
        Assert.Equal("ltfs.hash.sha1sum", xattrs[1].Key.Value);
        Assert.Equal(Convert.ToHexString(SHA1.HashData("abc"u8.ToArray())), xattrs[1].Value.Value);
    }

    [Fact]
    public async Task PerformReadTasks_ReadsFilesInTapeBlockOrder()
    {
        var tapeDrive = new ScriptedReadTapeDrive();
        var ltfs = CreateReadReadyLtfs(tapeDrive, blockSize: 4);

        var firstData = "BBBB"u8.ToArray();
        var secondData = "AAAA"u8.ToArray();
        tapeDrive.AddBlock("b", 10, firstData);
        tapeDrive.AddBlock("b", 20, secondData);

        AddLtfsReadSource(ltfs, "/later.bin", 20, secondData);
        AddLtfsReadSource(ltfs, "/earlier.bin", 10, firstData);

        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var laterTarget = Path.Combine(tempDirectory.FullName, "later.bin");
            var earlierTarget = Path.Combine(tempDirectory.FullName, "earlier.bin");

            ltfs.AddReadTask("/later.bin", laterTarget);
            ltfs.AddReadTask("/earlier.bin", earlierTarget);

            var success = await ltfs.PerformReadTasks();

            Assert.True(success);
            Assert.Equal("BBBB", await File.ReadAllTextAsync(earlierTarget));
            Assert.Equal("AAAA", await File.ReadAllTextAsync(laterTarget));
            Assert.Equal([10ul, 20ul], tapeDrive.LocateCalls.Select(call => call.Block).ToArray());

            var readTasks = ltfs.GetPendingTasks().OfType<ReadTask>().OrderBy(task => task.SequenceNumber).ToArray();
            Assert.All(readTasks, task => Assert.Equal(TaskExecutionStatus.Committed, task.Status));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task PerformReadTasks_IgnoresExistingTargetsWhenConfigured()
    {
        var tapeDrive = new ScriptedReadTapeDrive();
        var ltfs = CreateReadReadyLtfs(tapeDrive, blockSize: 4);
        ltfs.ReadTaskExistingFileMode = ReadTaskExistingFileMode.Ignore;

        var data = "DATA"u8.ToArray();
        tapeDrive.AddBlock("b", 15, data);
        AddLtfsReadSource(ltfs, "/existing.bin", 15, data);

        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var targetPath = Path.Combine(tempDirectory.FullName, "existing.bin");
            await File.WriteAllTextAsync(targetPath, "keep");

            ltfs.AddReadTask("/existing.bin", targetPath);

            var success = await ltfs.PerformReadTasks();

            Assert.True(success);
            Assert.Equal("keep", await File.ReadAllTextAsync(targetPath));
            Assert.Empty(tapeDrive.LocateCalls);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task PerformReadTasks_FailsAndRemovesPartialFileWhenHashMismatchOccurs()
    {
        var tapeDrive = new ScriptedReadTapeDrive();
        var ltfs = CreateReadReadyLtfs(tapeDrive, blockSize: 4);

        var data = "GOOD"u8.ToArray();
        tapeDrive.AddBlock("b", 30, data);

        var sourceFile = AddLtfsReadSource(ltfs, "/broken.bin", 30, data);
        sourceFile.ExtendedAttributes = new ExtendedAttributes
        {
            Xattrs = [
                new XAttr("ltfs.hash.sha1sum", Convert.ToHexString(SHA1.HashData("BAD!"u8.ToArray()))),
            ]
        };

        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var targetPath = Path.Combine(tempDirectory.FullName, "broken.bin");
            ltfs.AddReadTask("/broken.bin", targetPath);

            var success = await ltfs.PerformReadTasks();

            Assert.False(success);
            Assert.False(File.Exists(targetPath));

            var readTask = Assert.Single(ltfs.GetPendingTasks().OfType<ReadTask>());
            Assert.Equal(TaskExecutionStatus.Failed, readTask.Status);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Commit_WithReadType_CommitsOnlyReadTasks()
    {
        var tapeDrive = new ScriptedReadTapeDrive();
        var ltfs = CreateReadReadyLtfs(tapeDrive, blockSize: 4);
        var sourceData = "READ"u8.ToArray();
        tapeDrive.AddBlock("b", 40, sourceData);
        AddLtfsReadSource(ltfs, "/read.bin", 40, sourceData);

        var tempFile = Path.GetTempFileName();
        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllTextAsync(tempFile, "write-side");
            var readTarget = Path.Combine(tempDirectory.FullName, "read.bin");

            ltfs.AddFile(tempFile, "/write.bin");
            ltfs.AddReadTask("/read.bin", readTarget);

            var success = await ltfs.Commit(LtfsTaskQueueType.Read);

            Assert.True(success);
            Assert.Equal("READ", await File.ReadAllTextAsync(readTarget));

            var readTask = Assert.Single(ltfs.GetPendingReadTasks().OfType<ReadTask>());
            Assert.Equal(TaskExecutionStatus.Committed, readTask.Status);

            var writeTask = Assert.Single(ltfs.GetPendingWriteTasks().OfType<WriteTask>());
            Assert.Equal(TaskExecutionStatus.Pending, writeTask.Status);
        }
        finally
        {
            File.Delete(tempFile);
            tempDirectory.Delete(recursive: true);
        }
    }

    private static async Task InvokePrefetchTaskAsync(Ltfs.Ltfs ltfs, IReadOnlyList<WriteTask> writeTasks, SemaphoreSlim prefetchSemaphore, SemaphoreSlim prefetchSignal, int prefetchWindow, int prefetchThreshold, int directWriteThreshold)
    {
        var method = typeof(Ltfs.Ltfs).GetMethod("PrefetchTask", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PrefetchTask method was not found.");

        var task = (Task?)method.Invoke(ltfs, new object?[] { writeTasks, prefetchSemaphore, prefetchSignal, prefetchWindow, prefetchThreshold, directWriteThreshold })
            ?? throw new InvalidOperationException("PrefetchTask did not return a task.");

        await task;
    }

    private static System.Threading.Channels.ChannelReader<FileBuffer.SmallFileBufferItem>? GetBufferedReader(Ltfs.Ltfs ltfs, string path)
    {
        var field = typeof(Ltfs.Ltfs).GetField("fileBuffer", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("fileBuffer field was not found.");

        var buffer = (FileBuffer?)field.GetValue(ltfs)
            ?? throw new InvalidOperationException("fileBuffer was not initialized.");

        return buffer.GetReader(path);
    }

    private static async Task RemoveBufferedFileAsync(Ltfs.Ltfs ltfs, string path)
    {
        var field = typeof(Ltfs.Ltfs).GetField("fileBuffer", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("fileBuffer field was not found.");

        var buffer = (FileBuffer?)field.GetValue(ltfs)
            ?? throw new InvalidOperationException("fileBuffer was not initialized.");

        await buffer.RemoveAsync(path);
    }

    private static XAttr[] BuildHashXattrs(FileChecksumAlgorithm algorithm, byte[] data)
    {
        var ltfs = new Ltfs.Ltfs
        {
            ChecksumAlgorithm = algorithm,
        };

        var resetMethod = typeof(Ltfs.Ltfs).GetMethod("ResetHashes", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ResetHashes method was not found.");
        var appendMethod = typeof(Ltfs.Ltfs).GetMethod("AppendHashBytes", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("AppendHashBytes method was not found.");
        var buildMethod = typeof(Ltfs.Ltfs).GetMethod("BuildHashXattrs", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildHashXattrs method was not found.");
        var disposeMethod = typeof(Ltfs.Ltfs).GetMethod("DisposeHashes", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DisposeHashes method was not found.");

        resetMethod.Invoke(ltfs, null);
        try
        {
            appendMethod.Invoke(ltfs, new object?[] { data });
            return (XAttr[]?)buildMethod.Invoke(ltfs, null)
                ?? throw new InvalidOperationException("BuildHashXattrs returned null.");
        }
        finally
        {
            disposeMethod.Invoke(ltfs, null);
        }
    }

    private static Ltfs.Ltfs CreateReadReadyLtfs(ScriptedReadTapeDrive tapeDrive, int blockSize)
    {
        var ltfs = new Ltfs.Ltfs();
        ltfs.SetTapeDrive(tapeDrive);
        ltfs.LtfsDataTempIndexs.Clear();
        ltfs.LtfsDataTempIndexs.Add(LtfsIndex.Default());
        ltfs.LtfsLabelA = new LtfsLabel
        {
            Version = "2.4.0",
            Creator = "test",
            Formattime = DateTime.UtcNow,
            Volumeuuid = Guid.NewGuid(),
            Location = new Location { Partitions = { "a" } },
            Partitions = new Partitions { Index = "a", Data = "b" },
            Blocksize = blockSize,
            Compression = true,
        };
        ltfs.ReadTaskExistingFileMode = ReadTaskExistingFileMode.Overwrite;
        return ltfs;
    }

    private static LtfsFile AddLtfsReadSource(Ltfs.Ltfs ltfs, string sourcePath, ulong startBlock, byte[] data)
    {
        var index = ltfs.GetLatestIndex();
        var file = LtfsFile.Default();
        file.Name = Path.GetFileName(sourcePath);
        file.Length = (ulong)data.Length;
        file.ExtentInfo = new ExtentInfo
        {
            Extent = [
                new Extent
                {
                    Partition = "b",
                    StartBlock = startBlock,
                    FileOffset = 0,
                    ByteOffset = 0,
                    ByteCount = (ulong)data.Length,
                }
            ]
        };
        file.ExtendedAttributes = new ExtendedAttributes
        {
            Xattrs = [
                new XAttr("ltfs.hash.crc64sum", ComputeCrc64(data)),
            ]
        };

        var parentDirectory = EnsureDirectory(index.Directory, sourcePath);
        parentDirectory[file.Name.Value] = file;

        return file;
    }

    private static LtfsDirectory EnsureDirectory(LtfsDirectory root, string sourcePath)
    {
        var segments = sourcePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (current[segments[i]] is LtfsDirectory next)
            {
                current = next;
                continue;
            }

            next = LtfsDirectory.Default();
            next.Name = segments[i];
            current[segments[i]] = next;
            current = next;
        }

        return current;
    }

    private static string ComputeCrc64(byte[] data)
    {
        var crc64 = new Crc64();
        crc64.Append(data);
        return crc64.GetCurrentHashAsUInt64().ToString("X16");
    }

    private sealed class ScriptedReadTapeDrive : FakeTapeDrive
    {
        private readonly Dictionary<(byte Partition, ulong Block), byte[]> blocks = [];
        private byte currentPartition;
        private ulong currentBlock;

        public List<(byte Partition, ulong Block)> LocateCalls { get; } = [];

        public void AddBlock(string partition, ulong block, byte[] data)
        {
            blocks[(partition == "a" ? (byte)0 : (byte)1, block)] = data;
        }

        public override ushort Locate(ulong blockAddress, byte partitionNumber, LocateType locateType)
        {
            currentPartition = partitionNumber;
            currentBlock = blockAddress;
            LocateCalls.Add((partitionNumber, blockAddress));
            return 0;
        }

        public override PositionData ReadPosition()
        {
            return new PositionData
            {
                PartitionNumber = currentPartition,
                BlockNumber = currentBlock,
            };
        }

        public override byte[] ReadBlock(uint blockSizeLimit = 0x080000, bool truncate = false)
        {
            if (!blocks.TryGetValue((currentPartition, currentBlock), out var data))
                throw new IOException($"No tape block registered at partition {currentPartition}, block {currentBlock}.");

            currentBlock++;
            Sense = new byte[64];
            return data;
        }
    }

}

