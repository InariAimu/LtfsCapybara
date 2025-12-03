namespace LtoTape.CM;

public class UsagePage
{
    public uint Index { get; set; }
    public byte[] Data0 { get; set; } = [];
    public uint Data1 { get; set; }

    public override string ToString() => $"Key={Data1}, dataLen={Data0.Length}";
}
