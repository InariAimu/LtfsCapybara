namespace LtoTape.CM;

public class TapeStatus
{
    public uint ThreadCount { get; set; }
    public bool EncryptedData { get; set; } = false;
    public ushort LastLocation { get; set; }

    public void Parse(byte[] rawBytes, int offset, int gen, bool isCleaningTape)
    {
        if (offset > 0 && offset < rawBytes.Length - 1)
        {
            ThreadCount = BigEndianBitConverter.ToUInt32(rawBytes, offset + 12);
            if (gen >= 4)
            {
                if ((BigEndianBitConverter.ToUInt64(rawBytes, offset + 22) & 0xffff_ffff_ffff_0000) == 0xffff_ffff_ffff_0000)
                    EncryptedData = false;
                else
                    EncryptedData = true;
            }
            if (isCleaningTape)
                LastLocation = BigEndianBitConverter.ToUInt16(rawBytes, offset + 26);
        }
    }
}
