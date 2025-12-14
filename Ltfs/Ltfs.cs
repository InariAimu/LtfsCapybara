using System.Diagnostics.CodeAnalysis;

using Ltfs.Index;
using Ltfs.Label;

using TapeDrive;

namespace Ltfs;

public partial class Ltfs
{
    public const byte INDEX_PARTITION = 0;
    public const byte DATA_PARTITION = 1;

    public string Version { get; init; } = "2.4.0";

    public Vol1Label Vol1A { get; set; }
    public LtfsLabel? LtfsLabelA { get; set; } = null;

    public Vol1Label Vol1B { get; set; }
    public LtfsLabel? LtfsLabelB { get; set; } = null;

    public MAMAttributes LtfsMAMAttributes { get; set; } = new();

    public List<LtfsIndex> LtfsDataTempIndexs { get; set; } = new List<LtfsIndex>();

    public LtfsIndex? LtfsIndexA;
    public LtfsIndex? LtfsIndexB;

    public LtfsIndex? LtfsIndexCurr;

    public VCI VCI = new();

    public string Barcode { get; private set; } = string.Empty;


    // Backing field is created/managed internally; use null-forgiving to satisfy the
    // compiler while keeping the public view nullable for callers.
    private TapeDriveBase _tapeDrive = default!;

    public TapeDriveBase? TapeDrive => _tapeDrive;


    public int ExtraPartitionCount { get; set; } = 1;
    public bool DisablePartition { get; set; } = false;


    [MemberNotNull(nameof(_tapeDrive))]
    public bool LoadTape()
    {
        _tapeDrive ??= new LTOTapeDrive(@"\\.\Tape0", true);
        
        _tapeDrive.TestUnitReady();
        _tapeDrive.GetInquiry();

        _tapeDrive.Load();

        return true;
    }

    public bool UnloadTape()
    {
        if (_tapeDrive != null)
        {
            _tapeDrive.Unload();
            _tapeDrive.Dispose();
            _tapeDrive = default!;
        }
        return true;
    }

    public bool IsTapeLoaded()
    {
        if (_tapeDrive == null)
            return false;

        return true;
    }

    /// <summary>
    /// Allow tests to inject a fake or mock TapeDrive implementation.
    /// </summary>
    public void SetTapeDrive(TapeDriveBase drive)
    {
        _tapeDrive = drive ?? throw new ArgumentNullException(nameof(drive));
    }

    public bool Format(FormatParam formatParam)
    {
        if (!IsTapeLoaded())
            LoadTape();
        else
            _tapeDrive!.Rewind();

        var pos = _tapeDrive.ReadPosition();
        var modeData = _tapeDrive.ModeSense(0x11);
        var maxPartitions = modeData[2];
        ExtraPartitionCount = Math.Min(maxPartitions, ExtraPartitionCount);
        if (ExtraPartitionCount > 1)
            ExtraPartitionCount = 1;

        _tapeDrive.GlobalBlockSizeLimit = (uint)_tapeDrive.ReadBlockLimit().MaxBlockLength;
        var blocksize = Math.Min(formatParam.BlockSize, _tapeDrive.GlobalBlockSizeLimit);

        _tapeDrive.SetCapacity(formatParam.Capacity);

        // init tape
        _tapeDrive.ScsiCommand([4, 0, 0, 0, 0, 0]);

        // format
        _tapeDrive.SelectPartitionMode(modeData, maxPartitions, formatParam.P0Size, formatParam.P1Size);
        _tapeDrive.ScsiCommand([4, 0, 1, 0, 0, 0]);

        // set mam attributes
        LtfsMAMAttributes.ApplicationVendor.SetAsciiString("capybara");
        LtfsMAMAttributes.ApplicationName.SetAsciiString("LTFS capybara");
        LtfsMAMAttributes.ApplicationVersion.SetAsciiString("0.0.1");

        LtfsMAMAttributes.UserMediumTextLabel.SetTextString("");
        LtfsMAMAttributes.TextLocalizationIdentifier.SetBinary([0x81]);

        Barcode = formatParam.Barcode.ToUpperInvariant();
        LtfsMAMAttributes.Barcode.SetAsciiString(Barcode);

        LtfsMAMAttributes.ApplicationFormatVersion.SetAsciiString(Version);
        //LtfsMAMAttributes.MediaPool.SetTextString("");

        //LtfsMAMAttributes.MediumGloballyUniqueIdentifier.SetAsciiString("");
        //LtfsMAMAttributes.MediaPoolGloballyUniqueIdentifier.SetAsciiString("");

        LtfsMAMAttributes.WriteAll(writeFunc: _tapeDrive.SetMAMAttribute);

        //block size of 0: variable-length blocks
        _tapeDrive.SetBlockSize(0);

        // locate to data partition (part B)
        _tapeDrive.Locate(0, 1, LocateType.Block);
        pos = _tapeDrive.ReadPosition();

        // set encryption key
        _tapeDrive.SetEncryption(formatParam.EncryptionKey);

        // write vol1 label b
        Vol1B = new Vol1Label(Barcode, "LTFScapybara");
        _tapeDrive.Write(Vol1Label.ToByteArray(Vol1B), (int)blocksize);
        _tapeDrive.WriteFileMark();

        // write label b
        var volumeGuid = Guid.NewGuid();
        var formatTime = DateTime.UtcNow;
        LtfsLabelB = new LtfsLabel()
        {
            Version = Version,
            Creator = "LTFScapybara - test",
            Volumeuuid = volumeGuid,
            Formattime = formatTime,
            Blocksize = (int)blocksize,
            Location = new Location { Partitions = { "b" } },
            Partitions = new Partitions { Index = "a", Data = "b" },
            Compression = true,
        };
        _tapeDrive.Write(LtfsLabel.ToByteArray(LtfsLabelB), (int)blocksize);
        _tapeDrive.WriteFileMarks(2);

        // create index b
        var startBlock = _tapeDrive.ReadPosition().BlockNumber;
        LtfsIndexB = new LtfsIndex()
        {
            AllowPolicyUpdate = false,
            Creator = "LTFScapybara - test",
            VolumeUUID = volumeGuid,
            UpdateTime = formatTime,
            HighestFileUID = 1,
            GenerationNumber = 1,
            VolumeLockState = LockType.unlocked,
            Location = new TapePosition
            {
                Partition = "b",
                StartBlock = (uint)startBlock
            },
            PreviousGenerationLocation = null,
            Directory = new LtfsDirectory
            {
                Name = new NameType { Value = formatParam.VolumeName },
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
        LtfsDataTempIndexs.Add(LtfsIndexB);
        _tapeDrive.Write(LtfsIndex.ToByteArray(LtfsIndexB), (int)blocksize);
        _tapeDrive.WriteFileMark();



        // locate to index partition (part A)
        _tapeDrive.Locate(0, 0, LocateType.Block);
        pos = _tapeDrive.ReadPosition();

        // write vol1 label a
        Vol1A = new Vol1Label(volumeIdentifier: Barcode, ownerIdentifier: "capybara");
        _tapeDrive.Write(Vol1Label.ToByteArray(Vol1A), (int)blocksize);
        _tapeDrive.WriteFileMark();

        // write label a
        LtfsLabelA = (LtfsLabel?)LtfsLabelB.Clone();
        LtfsLabelA.Location.Partitions[0] = "a";
        _tapeDrive.Write(LtfsLabel.ToByteArray(LtfsLabelA), (int)blocksize);

        _tapeDrive.WriteFileMarks(2);

        // write index a
        startBlock = _tapeDrive.ReadPosition().BlockNumber;
        LtfsIndexA = (LtfsIndex)LtfsIndexB.Clone();
        LtfsIndexA.Location = new TapePosition
        {
            Partition = "a",
            StartBlock = (uint)startBlock
        };
        LtfsIndexA.PreviousGenerationLocation = new TapePosition
        {
            Partition = LtfsIndexB.Location.Partition,
            StartBlock = LtfsIndexB.Location.StartBlock,
        };
        LtfsDataTempIndexs.Add(LtfsIndexA);
        _tapeDrive.Write(LtfsIndex.ToByteArray(LtfsIndexA), (int)blocksize);

        _tapeDrive.WriteFileMark();

        // set datetime
        string time = formatTime.ToString("yyyyMMddhhmm");
        _tapeDrive.SetMAMAttribute(0x0804, time.PadRight(12), 12);

        // set vci
        _tapeDrive.Flush();

        var vcia = _tapeDrive.GetMAMAttribute(0x0009, 0);
        //var vcib = _tapeDrive.GetMAMAttribute(0x0009, 1);

        //VCI.FromByteArrayA(vcia.RawData);
        //VCI.FromByteArrayB(vcib.RawData);
        VCI.vciA = vcia.RawData[^4..];
        VCI.vciB = vcia.RawData[^4..];

        VCI.blockLocationA = LtfsIndexA.Location.StartBlock;
        VCI.blockLocationB = LtfsIndexB.Location.StartBlock;

        VCI.uuidA = volumeGuid.ToString();
        VCI.uuidB = volumeGuid.ToString();

        _tapeDrive.SetMAMAttribute(0x080c, VCI.BlockAToByteArray(), AttributeFormat.Binary, 0);
        _tapeDrive.SetMAMAttribute(0x080c, VCI.BlockBToByteArray(), AttributeFormat.Binary, 1);

        return true;
    }

    public Task<bool> FormatAsync(FormatParam param) => Task.Run(() => Format(param));

    public bool ReadLtfs()
    {
        try
        {
            ReadLtfsInfo();
            ReadNewestIndexFromIndexPartition();
            //ReadNewestIndexFromDataPartition();
        }
        catch (Exception ex)
        {
            Logger.Error($"Error reading LTFS: {ex.Message}");
            return false;
        }
        return true;
    }

    public bool WriteLtfsIndex()
    {
        Logger.Info("Writing LTFS Index to Data Partition...");
        WriteIndexToDataPartition();

        Logger.Info("Writing LTFS Index to Index Partition...");
        WriteIndexToIndexPartition();

        Logger.Info("Writing MAM Attribute (VCI)");
        var vcia = _tapeDrive.GetMAMAttribute(0x0009, 0);

        VCI.vciA = vcia.RawData[^4..];
        VCI.vciB = vcia.RawData[^4..];

        VCI.blockLocationA = LtfsIndexA.Location.StartBlock;
        VCI.blockLocationB = LtfsIndexB.Location.StartBlock;

        VCI.uuidA = LtfsLabelA.Volumeuuid.ToString();
        VCI.uuidB = LtfsLabelA.Volumeuuid.ToString();

        VCI.generation = LtfsIndexA.GenerationNumber;

        _tapeDrive.SetMAMAttribute(0x080c, VCI.BlockAToByteArray(), AttributeFormat.Binary, 0);
        _tapeDrive.SetMAMAttribute(0x080c, VCI.BlockBToByteArray(), AttributeFormat.Binary, 1);

        return true;
    }

    public bool ReadLtfsInfo()
    {
        if (!IsTapeLoaded())
            LoadTape();
        else
            _tapeDrive!.Rewind();

        Logger.Info("Reading LTFS Info...");

        var modeData = _tapeDrive.ModeSense(0x11);
        if (modeData.Length >= 4)
            ExtraPartitionCount = modeData[3];

        if (ExtraPartitionCount == 0)
            throw new Exception("No extra partition found. Is this tape LTFS formatted?");

        Barcode = _tapeDrive.ReadBarCode();
        Logger.Info($"Tape Barcode: {Barcode}");

        _tapeDrive.GlobalBlockSizeLimit = (uint)_tapeDrive.ReadBlockLimit().MaxBlockLength;

        Logger.Info("Reading Vol1 and LTFS Label from Index Partition...");
        _tapeDrive.Locate(0, INDEX_PARTITION, LocateType.Block);

        byte[] vol1data = _tapeDrive.ReadBlock();
        Vol1A = Vol1Label.FromByteArray(vol1data);

        _tapeDrive.Locate(1, INDEX_PARTITION, LocateType.FileMark);
        _tapeDrive.ReadFileMark();

        byte[] ltfsLabelData = _tapeDrive.ReadToFileMark();
        LtfsLabelA = LtfsLabel.FromByteArray(ltfsLabelData);
        if (LtfsLabelA is null)
            throw new Exception("Failed to read LTFS Label A");

        return true;
    }

    public bool ReadNewestIndexFromIndexPartition()
    {
        _tapeDrive.Locate(0, INDEX_PARTITION, LocateType.EOD);
        var pos = _tapeDrive.ReadPosition();

        _tapeDrive.Locate(pos.FileNumber - 1, INDEX_PARTITION, LocateType.FileMark);
        _tapeDrive.ReadFileMark();

        byte[] ltfsIndexData = _tapeDrive.ReadToFileMark();
        LtfsIndexA = LtfsIndex.FromByteArray(ltfsIndexData);

        _tapeDrive.SetBlockSize((ulong)LtfsLabelA.Blocksize);

        LtfsIndexB = (LtfsIndex)LtfsIndexA.Clone();
        LtfsIndexB.Location.Partition = LtfsIndexB.PreviousGenerationLocation.Partition;
        LtfsIndexB.Location.StartBlock = LtfsIndexB.PreviousGenerationLocation.StartBlock;

        LtfsDataTempIndexs.Add(LtfsIndexB);

        LtfsIndexCurr = (LtfsIndex)LtfsIndexA.Clone();

        return true;
    }

    public bool ReadNewestIndexFromDataPartition()
    {
        _tapeDrive.Locate(0, DATA_PARTITION, LocateType.EOD);
        var pos = _tapeDrive.ReadPosition();

        _tapeDrive.Locate(pos.FileNumber - 1, DATA_PARTITION, LocateType.FileMark);
        _tapeDrive.ReadFileMark();

        byte[] ltfsIndexData = _tapeDrive.ReadToFileMark();
        LtfsIndexB = LtfsIndex.FromByteArray(ltfsIndexData);

        LtfsIndexCurr = (LtfsIndex)LtfsIndexB.Clone();
        LtfsDataTempIndexs.Add(LtfsIndexCurr);

        return true;
    }

    public bool WriteIndexToDataPartition()
    {
        if (LtfsIndexCurr is null)
            throw new Exception("LtfsIndexCurr is null");

        _tapeDrive.Locate(0, DATA_PARTITION, LocateType.EOD);
        var pos = _tapeDrive.ReadPosition();

        var latestIndex = GetLatestIndex();
        var tmpIndex = (LtfsIndex)latestIndex.Clone();
        tmpIndex.GenerationNumber += 1;
        tmpIndex.UpdateTime = DateTime.UtcNow;
        tmpIndex.Location = new TapePosition
        {
            Partition = "b",
            StartBlock = (uint)_tapeDrive.ReadPosition().BlockNumber
        };

        tmpIndex.PreviousGenerationLocation = new TapePosition
        {
            Partition = latestIndex.Location.Partition,
            StartBlock = latestIndex.Location.StartBlock
        };

        LtfsDataTempIndexs.Add(tmpIndex);

        LtfsIndexB = tmpIndex;

        try
        {
            File.WriteAllText($"{Barcode}_P1_G{LtfsIndexB.GenerationNumber}_L{LtfsIndexB.Location.StartBlock}_T{DateTime.Now.Ticks}.xml",
                LtfsIndex.ToXml(LtfsIndexB));
        }
        catch
        {
            // ignore file write error
            Logger.Warn("Warning: Failed to write index XML 1 to local file.");
        }

        _tapeDrive.Write(LtfsIndex.ToByteArray(tmpIndex), LtfsLabelA.Blocksize);
        _tapeDrive.WriteFileMark();

        return true;
    }

    public bool WriteIndexToIndexPartition(bool lcgCompatible = false)
    {
        if (lcgCompatible)
        {
            _tapeDrive.Locate(3, INDEX_PARTITION, LocateType.FileMark);
            _tapeDrive.WriteFileMark();
        }
        else
            _tapeDrive.Locate(0, INDEX_PARTITION, LocateType.EOD);

        var pos = _tapeDrive.ReadPosition();
        var latestIndex = GetLatestIndex();
        var tmpIndex = (LtfsIndex)latestIndex.Clone();

        tmpIndex.Location = new TapePosition
        {
            Partition = "a",
            StartBlock = (uint)pos.BlockNumber,
        };
        tmpIndex.PreviousGenerationLocation = new TapePosition
        {
            Partition = latestIndex.Location.Partition,
            StartBlock = latestIndex.Location.StartBlock
        };

        LtfsIndexA = tmpIndex;

        try
        {
            File.WriteAllText($"{Barcode}_P0_G{LtfsIndexA.GenerationNumber}_L{LtfsIndexA.Location.StartBlock}_T{DateTime.Now.Ticks}.xml",
                LtfsIndex.ToXml(LtfsIndexA));
        }
        catch
        {
            // ignore file write error
            Logger.Warn("Warning: Failed to write index XML 0 to local file.");
        }

        _tapeDrive.Write(LtfsIndex.ToByteArray(tmpIndex), LtfsLabelA.Blocksize);
        _tapeDrive.WriteFileMark();
        _tapeDrive.Flush();
        return true;
    }

    public byte PartitionToNumber(string partition)
    {
        return partition switch
        {
            "a" => 0,
            "b" => 1,
            _ => 0
        };
    }


    public LtfsIndex GetLatestIndex()
    {
        LtfsDataTempIndexs = LtfsDataTempIndexs.OrderByDescending(x => x.GenerationNumber).ToList();
        return LtfsDataTempIndexs.First();
    }

}
