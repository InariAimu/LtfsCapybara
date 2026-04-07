using TapeDrive.Utils;

namespace TapeDrive.SCSICommands;

[MSBFirstStruct("If the rewind is successful, unsolicited positional sense will indicate that the tape is at BOM by theEOD bit being set and an additional sense code of 0004h (BOP).", ExplicitByteLength = 6)]
public class LoadUnload
{
    [Byte(0)]
    public byte Command = 0x1b;

    [Bit(1, 0)]
    [Metadata("Immediate", "", [
        "0\nStatus is returned after the rewind has completed.",
        "1\nThe drive first writes any unwritten buffered data to tape. lt then returns GOOD status to the hostbefore beginning the actual rewind operation."])]
    public bool Immed = false;

    [Bit(4, 0)]
    [Metadata("Load/Unload", "", [
        "0\nUnload the tape from the drive.",
        "1\nLoad the tape into the drive."])]
    public bool Load;

    [Bit(4, 1)]
    public bool ReTen;

    [Bit(4, 2)]
    public bool EOT = false;

    [Bit(4, 3)]
    [Metadata("Hold", "", [
        "0\nA normal load/ unload will be performed.",
        "1\nA load will cause the cartridge to be pulled in and seated in the drive, but the tape will not be threaded.An unload will cause the tape to be unthreaded, but the cartridge will not be ejected. In Hold position, the Cartridge Memory is accessible."])]
    public bool Hold;

    [Byte(5)]
    public byte Control = 0;
}
