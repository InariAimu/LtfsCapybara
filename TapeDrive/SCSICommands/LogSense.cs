using TapeDrive.Utils;

namespace TapeDrive.SCSICommands;

[MSBFirstStruct("If the rewind is successful, unsolicited positional sense will indicate that the tape is at BOM by theEOD bit being set and an additional sense code of 0004h (BOP).", ExplicitByteLength = 10)]
public class LogSense
{
    [Byte(0)]
    public byte Command = 0x4d;

    [Bit(1, 0)]
    public bool SP = false;

    [Bit(1, 1)]
    public bool PPC = false;

    [Byte(1, 6, 2)]
    [Metadata("Page Control", "The Page Control field defines the type of log parameter to be returned: \nThe PC field has no effect on the data returned when the selected log contains event or tracecodes rather than counts.",
    [
        "00b\nCurrent Threshold Values—any parameters in the log that are counters contain the maximum value that they can count to.",
        "01b\nCurrent Cumulative Values—any parameters in the log that are counters contain theircurrent counts. Note: Counts are reset to their default cumulative values (see below) following apower-on, reset or target/logical unit reset. Media related counts are also resetfollowing a load. For SAS drives,the counters are reset following a power-on reset or a soft resetinduced via the front panel.A LUN reset has no effect.",
        "10b\nDefault Threshold Values—same as the Current Threshold Values",
        "11b\nDefault Cumulative Values—any parameters in the log that are counters contain the initial values of those counters (set at power-on, reset or target/logical unit reset, and,in the case of media logs, load).",
    ])]
    public byte PC = 0b00;

    [Byte(1, 0, 6)]
    [Metadata("Parameter Pointer", "The Page Code field identifies which log page is being requested by the host. See \"SupportedLog Pages page\" (page 62) for the list of valid page codes.")]
    public byte PageCode = 0;

    [Word(5)]
    [Metadata("Parameter Pointer", "", [
        "0\nAll parameters are returned.",
        "n\nParameter data of a specified log page is returned in ascending order beginningfrom this code. lf this code is larger than the largest parameter in the page, the drive will return CHECK CONDITION with additional sense of 2400h (invalid field lnCDB)."])]
    public ushort ParameterPointer = 0;

    [Word(7)]
    [Metadata("Allocation Length", "The Allocation Length field specifies the maximum number of bytes of data that should bereturned to the host.The drive will return the entire log or Allocation Length bytes, whicheveris the lesser.")]
    public ushort AllocationLength = 0xffff;

    [Byte(9)]
    public byte Control = 0;
}
