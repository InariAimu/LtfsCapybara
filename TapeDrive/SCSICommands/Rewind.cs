using TapeDrive.Utils;

namespace TapeDrive.SCSICommands;

[MSBFirstStruct("If the rewind is successful, unsolicited positional sense will indicate that the tape is at BOM by theEOD bit being set and an additional sense code of 0004h (BOP).", ExplicitByteLength = 6)]
public class Rewind
{
    [Byte(0)]
    public byte Command = 0x01;

    [Bit(1, 0)]
    [Metadata("Immediate", "", [
        "0\nStatus is returned after the rewind has completed.",
        "1\nThe drive first writes any unwritten buffered data to tape. lt then returns GOOD status to the hostbefore beginning the actual rewind operation."])]
    public bool Immed;

    [Byte(5)]
    public byte Control = 0;
}
