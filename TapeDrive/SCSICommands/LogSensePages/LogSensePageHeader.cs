using TapeDrive.Utils;

namespace TapeDrive.SCSICommands.LogSensePages;

[MSBFirstStruct()]
public class LogSensePageHeader
{
    [Byte(0)]
    public byte PageCode = 0;

    [Word(2)]
    public ushort PageLength = 0;
}
