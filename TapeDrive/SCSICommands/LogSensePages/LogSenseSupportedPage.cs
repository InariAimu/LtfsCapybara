using TapeDrive.Utils;

namespace TapeDrive.SCSICommands.LogSensePages;

[MSBFirstStruct()]
public class LogSenseSupportedPage
{
    [Byte(0, 0, 6)]
    public byte PageCode = 0;

    [ByteList(2, LengthType.Word)]
    public byte[] SupportedPageCodes = [];
}
