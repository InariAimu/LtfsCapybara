using System.Text;

namespace LtoTape.CM;

public class Usage
{
    public ushort PageID { get; set; }
    public string DrvSN { get; set; } = string.Empty;
    public uint ThreadCount { get; set; }
    public Int64 SetsWritten { get; set; }
    public Int64 SetsRead { get; set; }
    public Int64 TotalSets { get; set; }
    public uint WriteRetries { get; set; }
    public uint ReadRetries { get; set; }
    public ushort UnRecovWrites { get; set; }
    public ushort UnRecovReads { get; set; }
    public ushort SuspendedWrites { get; set; }
    public ushort FatalSusWrites { get; set; }

    public ushort SuspendedAppendWrites { get; set; }
    public uint LP3Passes { get; set; }
    public uint MidpointPasses { get; set; }
    public byte MaxTapeTemp { get; set; }

    public Int64 CCQWriteFails { get; set; }
    public uint C2RecovErrors { get; set; }
    public uint DirectionChanges { get; set; }
    public uint TapePullingTime { get; set; }
    public uint TapeMetresPulled { get; set; }
    public uint Repositions { get; set; }
    public uint TotalLoadUnloads { get; set; }
    public uint StreamFails { get; set; }

    public ushort MaxDriveTemp { get; set; }
    public ushort MinDriveTemp { get; set; }



    public void Parse(int index, int driveSNLength, int[] offsets, Dictionary<int, UsagePage> usagePages, Manufacturer manufacturer, string mechRelatedInfoVendorID, int length)
    {
        PageID = BigEndianBitConverter.ToUInt16(usagePages[index].Data0, 0);
        ushort t = BigEndianBitConverter.ToUInt16(usagePages[index].Data0, 12);
        if (t != 0)
        {
            DrvSN = Encoding.ASCII.GetString(usagePages[index].Data0, 12, driveSNLength).TrimEnd();
            if (DrvSN.Length > 10)
            {
                DrvSN = DrvSN.Substring(DrvSN.Length - 10);
            }
        }
        else
            DrvSN = string.Empty;

        ThreadCount = BigEndianBitConverter.ToUInt32(usagePages[index].Data0, offsets[0]);
        SetsWritten = BigEndianBitConverter.ToInt64(usagePages[index].Data0, offsets[1]) - BigEndianBitConverter.ToInt64(usagePages[index + 1].Data0, offsets[1]);
        SetsRead = BigEndianBitConverter.ToInt64(usagePages[index].Data0, offsets[2]) - BigEndianBitConverter.ToInt64(usagePages[index + 1].Data0, offsets[2]);
        TotalSets = BigEndianBitConverter.ToInt64(usagePages[index].Data0, offsets[1]) - BigEndianBitConverter.ToInt64(usagePages[index + 1].Data0, offsets[2]);
        WriteRetries = BigEndianBitConverter.ToUInt32(usagePages[index].Data0, offsets[3]) - BigEndianBitConverter.ToUInt32(usagePages[index + 1].Data0, offsets[3]);
        ReadRetries = BigEndianBitConverter.ToUInt32(usagePages[index].Data0, offsets[4]) - BigEndianBitConverter.ToUInt32(usagePages[index + 1].Data0, offsets[4]);
        UnRecovWrites = (ushort)(BigEndianBitConverter.ToUInt16(usagePages[index].Data0, offsets[5]) - BigEndianBitConverter.ToUInt16(usagePages[index + 1].Data0, offsets[5]));
        UnRecovReads = (ushort)(BigEndianBitConverter.ToUInt16(usagePages[index].Data0, offsets[6]) - BigEndianBitConverter.ToUInt16(usagePages[index + 1].Data0, offsets[6]));
        SuspendedWrites = (ushort)(BigEndianBitConverter.ToUInt16(usagePages[index].Data0, offsets[7]) - BigEndianBitConverter.ToUInt16(usagePages[index + 1].Data0, offsets[7]));
        FatalSusWrites = (ushort)(BigEndianBitConverter.ToUInt16(usagePages[index].Data0, offsets[8]) - BigEndianBitConverter.ToUInt16(usagePages[index + 1].Data0, offsets[8]));

        if (manufacturer.Gen >= 5 && usagePages[index].Data0[76] > 0)
        {
            SuspendedAppendWrites = (ushort)(BigEndianBitConverter.ToUInt16(usagePages[index].Data0, 28) - BigEndianBitConverter.ToUInt16(usagePages[index + 1].Data0, 28));
            LP3Passes = BigEndianBitConverter.ToUInt32(usagePages[index].Data0, 68) - BigEndianBitConverter.ToUInt32(usagePages[index + 1].Data0, 68);
            MidpointPasses = BigEndianBitConverter.ToUInt32(usagePages[index].Data0, 72) - BigEndianBitConverter.ToUInt32(usagePages[index + 1].Data0, 72);
            MaxTapeTemp = usagePages[index].Data0[76];
        }

        if (mechRelatedInfoVendorID.Contains("HP"))
        {
            CCQWriteFails = BigEndianBitConverter.ToInt64(usagePages[index].Data0, length) - BigEndianBitConverter.ToInt64(usagePages[index + 1].Data0, length);
            C2RecovErrors = BigEndianBitConverter.ToUInt32(usagePages[index].Data0, length + 8) - BigEndianBitConverter.ToUInt32(usagePages[index + 1].Data0, length + 8);
            DirectionChanges = BigEndianBitConverter.ToUInt32(usagePages[index].Data0, length + 24) - BigEndianBitConverter.ToUInt32(usagePages[index + 1].Data0, length + 24);
            TapePullingTime = BigEndianBitConverter.ToUInt32(usagePages[index].Data0, length + 28) - BigEndianBitConverter.ToUInt32(usagePages[index + 1].Data0, length + 28);
            TapeMetresPulled = BigEndianBitConverter.ToUInt32(usagePages[index].Data0, length + 32);
            Repositions = BigEndianBitConverter.ToUInt32(usagePages[index].Data0, length + 36) - BigEndianBitConverter.ToUInt32(usagePages[index + 1].Data0, length + 36);
            TotalLoadUnloads = BigEndianBitConverter.ToUInt32(usagePages[index].Data0, length + 40);
            StreamFails = BigEndianBitConverter.ToUInt32(usagePages[index].Data0, length + 44) - BigEndianBitConverter.ToUInt32(usagePages[index + 1].Data0, length + 44);

            ushort maxDriveTemp = BigEndianBitConverter.ToUInt16(usagePages[index].Data0, 48);
            if (maxDriveTemp > 0)
                MaxDriveTemp = (ushort)(maxDriveTemp / 256);

            ushort minDriveTemp = BigEndianBitConverter.ToUInt16(usagePages[index].Data0, 48);
            if (minDriveTemp > 0)
                MinDriveTemp = (ushort)(minDriveTemp / 256);

            if (CCQWriteFails < 0) CCQWriteFails = 0;
        }
        else
        {
            CCQWriteFails = 0;
            C2RecovErrors = 0;
        }

    }
}

