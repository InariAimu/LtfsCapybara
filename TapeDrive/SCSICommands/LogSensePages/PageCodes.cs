
namespace TapeDrive.SCSICommands.LogSensePages;

public class PageCodes
{
    public const byte SupportedPages = 0x00;
    public const byte WriteErrorCounters = 0x02;
    public const byte ReadErrorCounters = 0x03;
    public const byte SequentialAccessDeviceLog = 0x0c;
    public const byte TemperatureLog = 0x0d;
    public const byte DTDStatusLog = 0x11;
    public const byte TapeAlertResponseLog = 0x12;
    public const byte RequestedRecoveryLog = 0x13;
    public const byte DeviceStatisicsLog = 0x14;
    public const byte ServiceBufferInformationLog = 0x15;
    public const byte TapeDiagnosticDatasLog = 0x16;
    public const byte VolumeStatisticsLog = 0x17;
    public const byte SASPortLog = 0x18;
    public const byte DataCompressionLog = 0x1b;
    public const byte TapeAlertLog = 0x2e;
    public const byte TapeUsageLog = 0x30;
    public const byte TapeCapacityLog = 0x31;
    public const byte DataCompression_HP_only_Log = 0x32;
    public const byte DeviceWellnessLog = 0x33;
    public const byte PerformanceLog = 0x34;
    public const byte DTDeviceErrorLog = 0x35;
    public const byte DeviceStatusLog = 0x3e;
}
