using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LtoTape.CM;

public class PageInfo
{
    public byte Version { get; set; }
    public ushort Offset { get; set; }
    public ushort Length { get; set; }
}
