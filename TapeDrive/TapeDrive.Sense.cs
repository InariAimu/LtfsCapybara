using TapeDrive.Utils;

namespace TapeDrive;

[MSBFirstStruct]
public class FixedFormatSenseData
{
    [Bit(0, 7)]
    [Metadata("Valid", "",
    ["1\nindicates that the information bytes contain valid information as defined in the SCSI specification."])]
    public bool Valid;

    [Byte(0, 0, 7)]
    [Metadata("Error Code", "",
    [
        "0x70\nIndicates that the error is current, that is, it is associated with the command for which CHECK CONDITION status has been reported.",
        "0x71\nIndicates that the error is deferred.A deferred error occurs when there is a failure foran operation that has already been terminated with a GOOD status, or when failureoccurs in \"cleanup\" activity following an operation that was terminated by BUS RELEASE.The command for which CHECK CONDITION status was reported is thereforeunlikely to be the cause of the deferred error. It simply gives the drive the opportunityto report CHECK CONDITION status for an error that already exists."])]
    public byte ErrorCode;

    [Bit(2, 7)]
    [Metadata("Mark", "", [
        "1\nThe Mark bit is set to 1 if a SPACE,READ or VERIFY command did not completebecause a filemark was read.This bit may only be set if the sense key is NO SENSE."
    ])]
    public bool Mark;

    [Bit(2, 6)]
    public bool EOM;

    [Bit(2, 5)]
    public bool ILI;

    [Byte(2, 0, 4)]
    public byte SenseKey;

    [Bytes(3, 4)]
    public byte[] InformationBytes = [];

    [Byte(7)]
    public byte AdditionalSenseLength;

    [Bytes(8, 4)]
    public byte[] CommandSpecificInformationBytes = [];

    [Byte(12)]
    public byte AdditionalSenseCode;

    [Byte(13)]
    public byte AdditionalSenseCodeQualifier;

    [Byte(14)]
    public byte FieldReplaceableUnitCode;

    [Bit(15, 7)]
    public bool SKSV;

    [Bit(15, 6)]
    public bool CPE;

    [Bit(15, 3)]
    public bool BPV;

    [Byte(15, 0, 3)]
    public byte BitPointer;

    [Word(16)]
    public ushort FieldPointerOrDriveErrorCode;

    [Bit(21, 3)]
    [Metadata("CLN", "", [
        "0\nThe drive is OK.",
        "1\nThe drive requires cleaning. The front panel LEDs will be displaying a \"clean me\" sequence or message. This bit is cleared to zero after a cleaning cartridge has beenused."
    ])]
    public bool CLN;
};



public partial class LTOTapeDrive : IDisposable
{
    public FixedFormatSenseData? ParsedSense { get; private set; }

    public void ParseFixedFormatSense()
    {
        if (Sense == null || Sense.Length == 0 || Sense[0] == 0)
        {
            ParsedSense = null;
            return;
        }

        ParsedSense = StructParser.Parse<FixedFormatSenseData>(Sense);
    }

    public void HandleSense()
    {

    }
};
