
using Ltfs;
using Ltfs.Index;
using Ltfs.Label;

using TapeDrive;

Ltfs.Ltfs lt = new();
lt.SetTapeDrive(new FakeTapeDrive());

// Initialize the test console logger. Adjust level as desired.
Log.SetLogger(new ConsoleLogger { Level = LogLevel.Info });

Logger.Info("Loading LTFS from tape...");
lt.LoadTape();

Logger.Info("Fake LTFS Index...");

lt.LtfsLabelA = new LtfsLabel()
{
    Version = "2.4.3",
    Creator = "LTFScapybara - test",
    Volumeuuid = Guid.NewGuid(),
    Formattime = DateTime.UtcNow,
    Blocksize = (int)524288,
    Location = new Location { Partitions = { "b" } },
    Partitions = new Partitions { Index = "a", Data = "b" },
    Compression = true,
};

lt.LtfsIndexB = new Ltfs.Index.LtfsIndex()
{
    AllowPolicyUpdate = false,
    Creator = "LTFScapybara - test",
    VolumeUUID = Guid.NewGuid(),
    UpdateTime = DateTime.UtcNow,
    HighestFileUID = 1,
    GenerationNumber = 1,
    VolumeLockState = LockType.unlocked,
    Location = new TapePosition
    {
        Partition = "b",
        StartBlock = (uint)0
    },
    PreviousGenerationLocation = null,
    Directory = new LtfsDirectory
    {
        Name = new NameType { Value = "VolumeName" },
        FileUID = 1,
        CreationTime = DateTime.UtcNow,
        ChangeTime = DateTime.UtcNow,
        ModifyTime = DateTime.UtcNow,
        AccessTime = DateTime.UtcNow,
        BackupTime = DateTime.UtcNow,
        ReadOnly = false,
        Contents = Array.Empty<object>(),
    },
};
lt.LtfsDataTempIndexs.Add((LtfsIndex)lt.LtfsIndexB.Clone());

lt.AddDirectory("", "");

Logger.Info("Writing files to tape...");
await lt.PerformWriteTasks();

Logger.Info("Writing LTFS Index...");
try
{
    lt.WriteLtfsIndex();
}
catch (Exception ex)
{
    Logger.Error("Error writing LTFS Index: " + ex.Message);
}

Logger.Info("Done.");
lt.TapeDrive?.Dispose();

Console.ReadKey();
