using TapeDrive.Utils;

namespace TapeDrive.SCSICommands.LogSensePages;

[MSBFirstStruct()]
public class PerformanceDataLogPage
{
    [Byte(0)]
    public byte PageCode = 0x34;

    [Word(2)]
    public ushort PageLength = 30;

    [Word(4)]
    public ushort ParameterCode;

    [Byte(7)]
    public byte ParameterLength;

    [Word(8)]
    public ushort RepositionsPer100MB;

    [Word(14)]
    public ushort DataRateIntoBuffer;

    [Word(20)]
    public ushort MaximumDataRate;

    [Word(26)]
    public ushort CurrentDataRate;

    [Word(32)]
    public ushort NativeDataRate;
}

public class PerformanceData
{
    public int RepositionsPer100MB { get; private set; }
    public double DataRateIntoBuffer { get; private set; }
    public double MaximumDataRate { get; private set; }
    public double CurrentDataRate { get; private set; }
    public double NativeDataRate { get; private set; }

    public double CompressionRatio => NativeDataRate > 0 ? DataRateIntoBuffer / CurrentDataRate : 1.0;

    public void ParseFromLogPageData(byte[] data)
    {
        if (data.Length < 18)
            throw new ArgumentException("Data length is insufficient for Performance Data Log Page.");

        var page = StructParser.Parse<PerformanceDataLogPage>(data);
        RepositionsPer100MB = page.RepositionsPer100MB;
        DataRateIntoBuffer = page.DataRateIntoBuffer / 10.0;
        MaximumDataRate = page.MaximumDataRate / 10.0;
        CurrentDataRate = page.CurrentDataRate / 10.0;
        NativeDataRate = page.NativeDataRate / 10.0;
    }
}
