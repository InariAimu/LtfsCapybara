namespace Ltfs;

public class FormatParam
{
    public string Barcode { get; set; } = string.Empty;
    public string VolumeName { get; set; } = string.Empty;
    public byte ExtraPartitionCount { get; set; } = 1;
    public ulong BlockSize { get; set; } = 524288;
    public bool ImmediateMode { get; set; } = true;
    public ushort Capacity { get; set; } = 0xffff;
    public ushort P0Size { get; set; } = 1;
    public ushort P1Size { get; set; } = 0xffff;
    public byte[]? EncryptionKey { get; set; } = null;
}
