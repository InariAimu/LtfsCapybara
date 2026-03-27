namespace LtoTape.CM;

public class EOD
{
    public uint DataSet { get; set; }
    public uint WrapNumber { get; set; }
    public ushort Validity { get; set; }
    public uint PhysicalPosition { get; set; }

    public void Parse(byte[] rawBytes, int offset)
    {
        if (offset > 0 && offset < rawBytes.Length - 36)
        {
            DataSet = BigEndianBitConverter.ToUInt32(rawBytes, offset + 24);
            WrapNumber = BigEndianBitConverter.ToUInt32(rawBytes, offset + 28);
            Validity = BigEndianBitConverter.ToUInt16(rawBytes, offset + 32);
            PhysicalPosition = BigEndianBitConverter.ToUInt32(rawBytes, offset + 36);
        }
    }
}
