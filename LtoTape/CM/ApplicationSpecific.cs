using System.Text;

namespace LtoTape.CM;

public class ApplicationSpecific
{
    public string BarCode { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;

    public void Parse(byte[] rawBytes, int offset, int length)
    {
        string s = Encoding.UTF8.GetString(rawBytes, offset + 4, 6);
        int index = 0;
        int attrLength = 0;
        if (s == "MAM001" || s == "MAM002")
        {
            index = 10;
        }
        while (index < length)
        {
            int attrId = BigEndianBitConverter.ToUInt16(rawBytes, offset + index);
            attrLength = BigEndianBitConverter.ToUInt16(rawBytes, offset + index + 2) & 0x0fff;

            if (attrId == 0x0fff || attrId == 0)
                break;

            if (attrId == 0x0806)
            {
                BarCode = Encoding.UTF8.GetString(rawBytes, offset + index + 4, attrLength).TrimEnd();
            }

            if (attrId == 0x0800)
                Vendor = Encoding.UTF8.GetString(rawBytes, offset + index + 4, attrLength).TrimEnd();

            if (attrId == 0x0801)
                Name = Encoding.UTF8.GetString(rawBytes, offset + index + 4, attrLength).TrimEnd();

            if (attrId == 0x0802)
                Version = Encoding.UTF8.GetString(rawBytes, offset + index + 4, attrLength).TrimEnd();

            index += 4 + attrLength;
        }
    }
}
