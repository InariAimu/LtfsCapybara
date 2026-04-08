using TapeDrive.Utils;

namespace TapeDrive.SCSICommands;

[MSBFirstStruct("WRITE FILEMARKS causes the specified number of filemarks to be written beginning at the currentlogical position on tape.\nlf the lmmed bit is set, GOOD status may be reported and the marks left in the data buffer.Otherwise, all buffered data and marks are written before status is reported.\nlf zero filemarks are to be written, the lmmed bit must be zero. The drive writes any buffered dataand marks to tape before reporting. This is the recommended way for a host to flush the buffer.", ExplicitByteLength = 6)]
public class WriteFilemarks
{
    [Byte(0)]
    public byte Command = 0x10;

    [Bit(1, 0)]
    [Metadata("Immediate", "", [
        "0\nStatus is returned after the rewind has completed.",
        "1\nThe drive returns GOOD status following the pre-execution checks (that is, before the commandstarts executing)."])]
    public bool Immed = false;

    [Bit(1, 1)]
    public bool WSmk = false;

    [DWord(2, 3)]
    public uint NumMarks = 0;

    [Byte(5)]
    public byte Control = 0;
}
