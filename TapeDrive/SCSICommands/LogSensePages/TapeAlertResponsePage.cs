using TapeDrive.Utils;

namespace TapeDrive.SCSICommands.LogSensePages;

[MSBFirstStruct()]
public class TapeAlertResponsePage
{
    [Byte(0)]
    public byte PageCode = 0x12;

    [Word(2)]
    public ushort PageLength = 0x000c;

    [TLVList(0, 1, 64)]
    public byte[] ParameterValue = new byte[64];
}
