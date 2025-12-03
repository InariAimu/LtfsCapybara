using System.Text;

using LtoTape;

namespace Ltfs;

public class VCI
{
    public byte[] head = [8, 0, 0, 0, 0];

    public byte[] vciA = [0, 0, 0, 0];
    public byte[] vciB = [0, 0, 0, 0];

    public UInt64 generation = 0;

    public UInt64 blockLocationA = 0;
    public UInt64 blockLocationB = 0;

    public byte[] mid = [0, 0x2b, 0x4c, 0x54, 0x46, 0x53, 0];

    public string uuidA = "";
    public string uuidB = "";

    public byte[] end = [0, 1];

    public void FromByteArrayA(byte[] data)
    {
        head = data[0..5];
        vciA = data[5..9];
        generation = BigEndianBitConverter.ToUInt64(data, 9);
        blockLocationA = BigEndianBitConverter.ToUInt64(data, 17);
        mid = data[25..32];
        uuidA = Encoding.ASCII.GetString(data, 32, 36).Trim();
        end = data[68..70];
    }

    public void FromByteArrayB(byte[] data)
    {
        head = data[0..5];
        vciB = data[5..9];
        generation = BigEndianBitConverter.ToUInt64(data, 9);
        blockLocationB = BigEndianBitConverter.ToUInt64(data, 17);
        mid = data[25..32];
        uuidB = Encoding.ASCII.GetString(data, 32, 36).Trim();
        end = data[68..70];
    }

    public byte[] BlockAToByteArray()
    {
        return [
                .. head,
                .. vciA,
                .. BigEndianBitConverter.GetBytes(generation),
                .. BigEndianBitConverter.GetBytes(blockLocationA),
                .. mid,
                .. Encoding.ASCII.GetBytes(uuidA.ToString().PadRight(36)[..36]),
                .. end
            ];
    }

    public byte[] BlockBToByteArray()
    {
        return [
                .. head,
                .. vciB,
                .. BigEndianBitConverter.GetBytes(generation),
                .. BigEndianBitConverter.GetBytes(blockLocationB),
                .. mid,
                .. Encoding.ASCII.GetBytes(uuidB.ToString().PadRight(36)[..36]),
                .. end
            ];
    }

}
