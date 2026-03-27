using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LtoTape.CM;

public enum WrapType
{
    Normal,
    Empty,
    EOD,
    Guard,
    Unused,
};

public class WrapInfo
{
    public int Index { get; set; }
    public uint StartBlock { get; set; }
    public uint EndBlock { get; set; }
    public int FileMarkCount => HOWFMCnt + EOWFMCnt;
    public int RecCount => HOWRecCnt + EOWRecCnt;
    public uint Set { get; set; }
    public float Capacity { get; set; }
    public WrapType Type { get; set; } = WrapType.Normal;

    public int WritePass { get; set; }
    public uint DataSetId {  get; set; }
    public int HOWRecCnt {  get; set; }
    public int EOWRecCnt {  set; get; }
    public int HOWFMCnt { get; set; }
    public int EOWFMCnt { get; set; }
    public int FMMap { get; set; }
    public uint CRC { get; set; }

    public override string ToString() => $"{Index} {Set} {Capacity:f2} {Type}";

    public void Parse(byte[] rawBytes, int offset, int wrapIndex, ref uint lastId, ref uint lastSetId, 
        Dictionary<int, EOD> eods, int partitionCount, TapePhysicInfo tapeInfo)
    {
        Index = wrapIndex;

        uint setId = BigEndianBitConverter.ToUInt32(rawBytes, offset + 4);

        if (setId == 0xffffffff || setId == 0xfffffffe)
        {
            Set = 0;
            lastSetId = 0;
        }
        else
        {
            Set = setId - lastSetId;
            lastSetId = setId;
        }

        if (setId == 0xffffffff)
        {
            Type = WrapType.Empty;
        }
        else if (setId == 0xfffffffe)
        {
            Type = WrapType.Guard;
        }
        else if (setId == 0)
        {
            Type = WrapType.Unused;
        }
        else
        {
            int set = 0;
            for (int partIndex = 0; partIndex < partitionCount; partIndex++)
            {
                if (eods.ContainsKey(partIndex) && eods[partIndex].Validity > 0 && eods[partIndex].WrapNumber == wrapIndex)
                {
                    Type = WrapType.EOD;
                    set = 1;
                }
            }
            if (set == 0)
            {
                Capacity = (100 - 100 * (1 - (float)(setId - lastId) / tapeInfo.SetsPerWrap));
                lastId = setId;
            }
        }

        WritePass = BigEndianBitConverter.ToInt32(rawBytes, offset);
        HOWRecCnt = BigEndianBitConverter.ToInt32(rawBytes, offset + 8);
        EOWRecCnt = BigEndianBitConverter.ToInt32(rawBytes, offset + 12);
        HOWFMCnt = BigEndianBitConverter.ToInt32(rawBytes, offset + 16);
        EOWFMCnt = BigEndianBitConverter.ToInt32(rawBytes, offset + 20);
        FMMap = BigEndianBitConverter.ToInt32(rawBytes, offset + 24);
        CRC = BigEndianBitConverter.ToUInt32(rawBytes, offset + 28);
    }
}
