using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LtoTape;

public class MAMAttribute
{
    public ushort ID { get; set; }
    public string ID_Hex => ((ID >> 8) & 0xff).ToString("X2") + (ID & 0xff).ToString("X2");
    public byte[] RawData { get; set; } = [];
    public int Length => RawData.Length;

    public string DataAsString
    {
        get
        {
            try
            {
                return Encoding.ASCII.GetString(RawData);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
    public UInt64 AsUInt64
    {
        get
        {
            if (RawData.Length == 1)
                return RawData[0];
            else if (RawData.Length == 2)
                return BigEndianBitConverter.ToUInt16(RawData, 0);
            else if (RawData.Length == 4)
                return BigEndianBitConverter.ToUInt32(RawData, 0);
            else if (RawData.Length == 8)
                return BigEndianBitConverter.ToUInt64(RawData, 0);
            else
                return 0;
        }
    }


}
