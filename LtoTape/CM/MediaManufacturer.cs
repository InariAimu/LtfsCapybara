using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LtoTape.CM;

public class MediaManufacturer
{
    public string MfgDate { get; set; } = "";
    public string Vendor { get; set; } = "";

    public void Parse(byte[] data, int startOffset)
    {
        MfgDate = Encoding.ASCII.GetString(data, startOffset + 4, 8);
        Vendor = Encoding.ASCII.GetString(data, startOffset + 12, 8);
    }
}

