using TapeDrive.Utils;

namespace TapeDrive.SCSICommands;

[MSBFirstStruct("", ExplicitByteLength = 6)]
public class ReceiveDiagnosticResults
{
    [Byte(0)]
    public byte Command = 0x1c;

    [Bit(1, 0)]
    public bool PCV = false;


    [Byte(2)]
    [Metadata("Page Code", "Identifier for the diagnostic information page to be returned.70h returns the Self-Test page.")]
    public byte PageCode = 0;

    [Word(3)]
    [Metadata("Allocation Length", "The number of bytes which the host has allocated for returned diagnostic data. The drivewill return allocation length bytes or the amount of data that is available, whichever is least.")]
    public ushort AllocationLength = 0xffff;

    [Byte(5)]
    public byte Control = 0;
}
