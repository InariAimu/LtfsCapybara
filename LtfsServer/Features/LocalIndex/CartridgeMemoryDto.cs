using LtoTape;
using LtoTape.CM;

namespace LtfsServer.Features.LocalIndex;

public sealed class CartridgeMemoryDto
{
    public ApplicationSpecificDto ApplicationSpecific { get; init; } = new();
    public ManufacturerDto Manufacturer { get; init; } = new();
    public MediaManufacturerDto MediaManufacturer { get; init; } = new();
    public Dictionary<string, UsageInfoDto> Usages { get; init; } = [];
    public Dictionary<string, EodDto> EoDs { get; init; } = [];
    public Dictionary<string, PartitionInfoDto> Partitions { get; init; } = [];
    public List<WrapInfoDto> Wraps { get; init; } = [];

    public static CartridgeMemoryDto From(CartridgeMemory cm) => new()
    {
        ApplicationSpecific = new()
        {
            BarCode = cm.ApplicationSpecific.BarCode,
            Vendor = cm.ApplicationSpecific.Vendor,
            Name = cm.ApplicationSpecific.Name,
            Version = cm.ApplicationSpecific.Version,
        },
        Manufacturer = new()
        {
            TapeVendor = cm.Manufacturer.TapeVendor,
            CartridgeSN = cm.Manufacturer.CartridgeSN,
            CartridgeType = cm.Manufacturer.CartridgeType,
            Format = cm.Manufacturer.Format,
            Gen = cm.Manufacturer.Gen,
            MfgDate = cm.Manufacturer.MfgDate,
            TapeLength = cm.Manufacturer.TapeLength,
            MediaCode = cm.Manufacturer.MediaCode,
            ParticleType = (int)cm.Manufacturer.ParticleType,
            IsCleaningTape = cm.Manufacturer.IsCleaningTape,
            TapePhysicInfo = new()
            {
                NWraps = cm.Manufacturer.TapePhysicInfo.NWraps,
                SetsPerWrap = cm.Manufacturer.TapePhysicInfo.SetsPerWrap,
                TapDirLength = cm.Manufacturer.TapePhysicInfo.TapDirLength,
                KBytesPerSet = cm.Manufacturer.TapePhysicInfo.KBytesPerSet,
                LifeInFVE = cm.Manufacturer.TapePhysicInfo.LifeInFVE,
            },
        },
        MediaManufacturer = new()
        {
            MfgDate = cm.MediaManufacturer.MfgDate,
            Vendor = cm.MediaManufacturer.Vendor,
        },
        Usages = cm.Usages.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => new UsageInfoDto
            {
                DrvSN = kvp.Value.DrvSN,
                ThreadCount = kvp.Value.ThreadCount,
                LifeSetsWritten = kvp.Value.LifeSetsWritten,
                LifeSetsRead = kvp.Value.LifeSetsRead,
                LifeWriteRetries = kvp.Value.LifeWriteRetries,
                LifeReadRetries = kvp.Value.LifeReadRetries,
                LifeUnRecovWrites = kvp.Value.LifeUnRecovWrites,
                LifeUnRecovReads = kvp.Value.LifeUnRecovReads,
                LifeSuspendedWrites = kvp.Value.LifeSuspendedWrites,
                LifeSuspendedAppendWrites = kvp.Value.LifeSuspendedAppendWrites,
                LifeFatalSusWrites = kvp.Value.LifeFatalSusWrites,
            }),
        EoDs = cm.EODs.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => new EodDto
            {
                DataSet = kvp.Value.DataSet,
                WrapNumber = kvp.Value.WrapNumber,
                Validity = kvp.Value.Validity,
                PhysicalPosition = kvp.Value.PhysicalPosition,
            }),
        Partitions = cm.Partitions.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => new PartitionInfoDto
            {
                Index = kvp.Key,
                WrapCount = kvp.Value.WrapCount,
                AllocatedSize = kvp.Value.AllocatedSize,
                UsedSize = kvp.Value.UsedSize,
                EstimatedLossSize = kvp.Value.EstimatedLossSize,
            }),
        Wraps = cm.Wraps.Select(w => new WrapInfoDto
        {
            Index = w.Index,
            StartBlock = w.StartBlock,
            EndBlock = w.EndBlock,
            FileMarkCount = w.FileMarkCount,
            Set = w.Set,
            Type = (int)w.Type,
            Capacity = w.Capacity,
        }).ToList(),
    };
}

public sealed class ApplicationSpecificDto
{
    public string BarCode { get; init; } = "";
    public string Vendor { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
}

public sealed class ManufacturerDto
{
    public string TapeVendor { get; init; } = "";
    public string CartridgeSN { get; init; } = "";
    public int CartridgeType { get; init; }
    public string Format { get; init; } = "";
    public int Gen { get; init; }
    public string MfgDate { get; init; } = "";
    public int TapeLength { get; init; }
    public int MediaCode { get; init; }
    public int ParticleType { get; init; }
    public bool IsCleaningTape { get; init; }
    public TapePhysicInfoDto TapePhysicInfo { get; init; } = new();
}

public sealed class TapePhysicInfoDto
{
    public int NWraps { get; init; }
    public int SetsPerWrap { get; init; }
    public int TapDirLength { get; init; }
    public int KBytesPerSet { get; init; }
    public int LifeInFVE { get; init; }
}

public sealed class MediaManufacturerDto
{
    public string MfgDate { get; init; } = "";
    public string Vendor { get; init; } = "";
}

public sealed class EodDto
{
    public uint DataSet { get; init; }
    public uint WrapNumber { get; init; }
    public ushort Validity { get; init; }
    public uint PhysicalPosition { get; init; }
}

public sealed class PartitionInfoDto
{
    public int Index { get; init; }
    public int WrapCount { get; init; }
    public long AllocatedSize { get; init; }
    public long UsedSize { get; init; }
    public long EstimatedLossSize { get; init; }
}

public sealed class UsageInfoDto
{
    public string DrvSN { get; init; } = "";
    public uint ThreadCount { get; init; }
    public long LifeSetsWritten { get; init; }
    public long LifeSetsRead { get; init; }
    public int LifeWriteRetries { get; init; }
    public int LifeReadRetries { get; init; }
    public int LifeUnRecovWrites { get; init; }
    public int LifeUnRecovReads { get; init; }
    public int LifeSuspendedWrites { get; init; }
    public int LifeSuspendedAppendWrites { get; init; }
    public int LifeFatalSusWrites { get; init; }
}

public sealed class WrapInfoDto
{
    public int Index { get; init; }
    public uint StartBlock { get; init; }
    public uint EndBlock { get; init; }
    public int FileMarkCount { get; init; }
    public uint Set { get; init; }
    public int Type { get; init; }
    public float Capacity { get; init; }
}
