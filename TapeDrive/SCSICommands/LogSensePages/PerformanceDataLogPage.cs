using TapeDrive.Utils;

namespace TapeDrive.SCSICommands.LogSensePages;

[MSBFirstStruct()]
public class PerformanceDataLogPage
{
    [Byte(0)]
    public byte PageCode;

    [Word(2)]
    public ushort PageLength;

    [Word(4)]
    public ushort ParameterCode = 0x0034;

    [Byte(7)]
    public byte ParameterLength;

    [Word(8)]
    public ushort RepositionsPer100MB;

    [Word(10)]
    public ushort DataRateIntoBuffer;

    [Word(12)]
    public ushort MaximumDataRate;

    [Word(14)]
    public ushort CurrentDataRate;

    [Word(16)]
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
