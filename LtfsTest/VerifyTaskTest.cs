using System.IO.Hashing;

using Ltfs;
using Ltfs.Index;
using Ltfs.Label;
using Ltfs.Tasks;

using TapeDrive;

namespace LtfsTest;

public class VerifyTaskTest
{
    [Fact]
    public void AddVerifyTask_UsesDedicatedQueue()
    {
        var ltfs = CreateLtfsWithIndex();
        AddLtfsReadSource(ltfs, "/alpha.bin", 10, [1, 2, 3, 4]);

        ltfs.AddVerifyTask("/alpha.bin");

        var verifyTasks = ltfs.GetPendingVerifyTasks();
        Assert.Single(verifyTasks);
        Assert.IsType<VerifyTask>(verifyTasks[0]);
        Assert.Empty(ltfs.GetPendingReadTasks());
        Assert.Empty(ltfs.GetPendingWriteTasks());
        Assert.Single(ltfs.GetPendingTasks());
    }

    [Fact]
    public async Task PerformVerifyTasks_OrdersReadsByBlockAndCommitsTasks()
    {
        var tapeDrive = new ScriptedReadTapeDrive();
        var ltfs = CreateReadReadyLtfs(tapeDrive, blockSize: 4);

        AddLtfsReadSource(ltfs, "/later.bin", 10, [9, 10, 11, 12]);
        AddLtfsReadSource(ltfs, "/earlier.bin", 4, [1, 2, 3, 4]);

        tapeDrive.AddBlock("b", 10, [9, 10, 11, 12]);
        tapeDrive.AddBlock("b", 4, [1, 2, 3, 4]);

        ltfs.AddVerifyTask("/later.bin");
        ltfs.AddVerifyTask("/earlier.bin");

        var result = await ltfs.PerformVerifyTasks();

        Assert.True(result);
        var firstLocate = ((byte)1, 4ul);
        var secondLocate = ((byte)1, 10ul);
        Assert.Equal(firstLocate, tapeDrive.LocateCalls[0]);
        Assert.Equal(secondLocate, tapeDrive.LocateCalls[1]);
        foreach (var task in ltfs.GetPendingVerifyTasks().Cast<VerifyTask>())
        {
            Assert.Equal(TaskExecutionStatus.Committed, task.Status);
            Assert.False(task.VerificationSkipped);
            Assert.True(task.VerificationPassed);
            Assert.NotNull(task.ExpectedCrc64);
            Assert.NotNull(task.ActualCrc64);
        }
    }

    [Fact]
    public async Task PerformVerifyTasks_RecordsFailureWithoutThrowing()
    {
        var tapeDrive = new ScriptedReadTapeDrive();
        var ltfs = CreateReadReadyLtfs(tapeDrive, blockSize: 4);
        AddLtfsReadSource(ltfs, "/broken.bin", 7, [5, 6, 7, 8], expectedCrc64: "0000000000000000");
        tapeDrive.AddBlock("b", 7, [5, 6, 7, 8]);

        ltfs.AddVerifyTask("/broken.bin");

        var result = await ltfs.PerformVerifyTasks();

        Assert.False(result);
        var task = Assert.IsType<VerifyTask>(ltfs.GetPendingVerifyTasks().Single());
        Assert.Equal(TaskExecutionStatus.Failed, task.Status);
        Assert.False(task.VerificationSkipped);
        Assert.False(task.VerificationPassed);
        Assert.Equal("0000000000000000", task.ExpectedCrc64);
        Assert.NotNull(task.ActualCrc64);
        Assert.Contains("CRC64 mismatch", task.VerificationMessage);
    }

    [Fact]
    public async Task PerformVerifyTasks_SkipsZeroLengthFiles()
    {
        var tapeDrive = new ScriptedReadTapeDrive();
        var ltfs = CreateReadReadyLtfs(tapeDrive, blockSize: 4);
        AddLtfsReadSource(ltfs, "/empty.bin", 3, []);

        ltfs.AddVerifyTask("/empty.bin");

        var result = await ltfs.PerformVerifyTasks();

        Assert.True(result);
        var task = Assert.IsType<VerifyTask>(ltfs.GetPendingVerifyTasks().Single());
        Assert.Equal(TaskExecutionStatus.Committed, task.Status);
        Assert.True(task.VerificationSkipped);
        Assert.Null(task.VerificationPassed);
        Assert.Equal("Skipped zero-length file.", task.VerificationMessage);
        Assert.Empty(tapeDrive.LocateCalls);
    }

    [Fact]
    public async Task PerformVerifyTasks_SkipsFilesWithoutCrc64()
    {
        var tapeDrive = new ScriptedReadTapeDrive();
        var ltfs = CreateReadReadyLtfs(tapeDrive, blockSize: 4);
        AddLtfsReadSource(ltfs, "/sha-only.bin", 3, [1, 2, 3, 4], expectedCrc64: null, includeSha1: true);

        ltfs.AddVerifyTask("/sha-only.bin");

        var result = await ltfs.PerformVerifyTasks();

        Assert.True(result);
        var task = Assert.IsType<VerifyTask>(ltfs.GetPendingVerifyTasks().Single());
        Assert.Equal(TaskExecutionStatus.Committed, task.Status);
        Assert.True(task.VerificationSkipped);
        Assert.Null(task.VerificationPassed);
        Assert.Equal("Skipped file without CRC64 hash.", task.VerificationMessage);
        Assert.Empty(tapeDrive.LocateCalls);
    }

    [Fact]
    public async Task PerformVerifyTasks_PrefersCrc64WhenMultipleHashesExist()
    {
        var tapeDrive = new ScriptedReadTapeDrive();
        var ltfs = CreateReadReadyLtfs(tapeDrive, blockSize: 4);
        AddLtfsReadSource(ltfs, "/multi-hash.bin", 9, [1, 2, 3, 4], expectedCrc64: ComputeCrc64([1, 2, 3, 4]), includeSha1: true, expectedSha1: "0000000000000000000000000000000000000000");
        tapeDrive.AddBlock("b", 9, [1, 2, 3, 4]);

        ltfs.AddVerifyTask("/multi-hash.bin");

        var result = await ltfs.PerformVerifyTasks();

        Assert.True(result);
        var task = Assert.IsType<VerifyTask>(ltfs.GetPendingVerifyTasks().Single());
        Assert.Equal(TaskExecutionStatus.Committed, task.Status);
        Assert.False(task.VerificationSkipped);
        Assert.True(task.VerificationPassed);
        Assert.Equal(ComputeCrc64([1, 2, 3, 4]), task.ExpectedCrc64);
    }

    private static Ltfs.Ltfs CreateLtfsWithIndex()
    {
        var ltfs = new Ltfs.Ltfs();
        ltfs.LtfsDataTempIndexs.Clear();
        ltfs.LtfsDataTempIndexs.Add(LtfsIndex.Default());
        return ltfs;
    }

    private static Ltfs.Ltfs CreateReadReadyLtfs(ScriptedReadTapeDrive tapeDrive, int blockSize)
    {
        var ltfs = CreateLtfsWithIndex();
        ltfs.SetTapeDrive(tapeDrive);
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

    private static LtfsFile AddLtfsReadSource(Ltfs.Ltfs ltfs, string sourcePath, ulong startBlock, byte[] data, string? expectedCrc64 = null, bool includeSha1 = false, string? expectedSha1 = null)
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

        var xattrs = new List<XAttr>();
        var resolvedCrc64 = expectedCrc64 ?? (includeSha1 ? null : ComputeCrc64(data));
        if (resolvedCrc64 is not null)
        {
            xattrs.Add(new XAttr("ltfs.hash.crc64sum", resolvedCrc64));
        }

        if (includeSha1)
        {
            xattrs.Add(new XAttr("ltfs.hash.sha1sum", expectedSha1 ?? ComputeSha1(data)));
        }

        if (xattrs.Count > 0)
        {
            file.ExtendedAttributes = new ExtendedAttributes
            {
                Xattrs = [.. xattrs]
            };
        }

        index.Directory[file.Name.Value] = file;
        return file;
    }

    private static string ComputeCrc64(byte[] data)
    {
        var crc64 = new Crc64();
        crc64.Append(data);
        return crc64.GetCurrentHashAsUInt64().ToString("X16");
    }

    private static string ComputeSha1(byte[] data)
    {
        return Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(data));
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