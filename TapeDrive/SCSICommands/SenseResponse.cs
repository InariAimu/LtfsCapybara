using TapeDrive.Utils;

namespace TapeDrive.SCSICommands;

[MSBFirstStruct]
public class SenseResponse
{
    [Word(7)]
    [Metadata("Status Qualifier", "Additional status qualifier returned by the drive.")]
    public ushort StatusQualifier = 0x0000;

    [Byte(10, 0, 2)]
    [Metadata("DATAPRES", "Data presence bits from the sense response header.")]
    public byte DATAPRES;

    [Byte(11)]
    [Metadata("Status", "Response status byte.")]
    public byte Status;

    [DWord(16)]
    [Metadata("Sense Data Length", "Number of bytes contained in SenseData.")]
    public uint SenseDataLength = 0;

    [DWord(20)]
    [Metadata("Response Data Length", "Number of bytes contained in ResponseData.")]
    public uint ResponseDataLength = 0;

    [RefByteList(24, nameof(ResponseDataLength))]
    [Metadata("Response Data", "Response payload described by ResponseDataLength.")]
    public byte[] ResponseData = [];

    [RefByteList(-1, nameof(SenseDataLength))]
    [Metadata("Sense Data", "Sense payload described by SenseDataLength.")]
    public byte[] SenseData = [];
}
