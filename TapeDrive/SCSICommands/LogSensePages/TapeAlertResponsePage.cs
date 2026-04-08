using TapeDrive.Utils;

namespace TapeDrive.SCSICommands.LogSensePages;

[MSBFirstStruct()]
public class TapeAlertResponsePage
{
    [Byte(0)]
    public byte PageCode = 0x12;

    [Word(2)]
    public ushort PageLength = 0x000c;

    [Word(4)]
    public ushort ParameterCode = 0x0000;

    [Byte(7)]
    public byte ParameterLength = 0x00;

    [Bytes(8, 8)]
    public byte[] ParameterValue = new byte[8];
}
