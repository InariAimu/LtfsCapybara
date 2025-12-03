namespace TapeDrive;

public class PositionData
{
    public bool BOP { get; set; } = false;
    public bool EOP { get; set; } = false;
    public bool MPU { get; set; } = false;
    public byte PartitionNumber { get; set; } = 0;
    public UInt64 BlockNumber { get; set; } = 0;
    public UInt64 FileNumber { get; set; } = 0;
    public UInt64 SetNumber { get; set; } = 0;
    public UInt16 AddSenseKey { get; set; } = 0;
    public bool EOD => (AddSenseKey == 5);
}
