using TapeDrive.Utils;

namespace TapeDrive.SCSICommands.LogSensePages;

[MSBFirstStruct()]
public class WriteErrorRateLogPage
{
    [Byte(3, 4096)]
    public byte[] data = [];
}
