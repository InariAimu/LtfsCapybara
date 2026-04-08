

namespace TapeDrive.SCSICommands.LogSensePages;

public enum TapeAlertLevel
{
    Information, Warning, Critical
}

public class TapeAlertItem
{
    public int BitPosition { get; init; }
    public string Name { get; init; } = "";
    public TapeAlertLevel AlertLevel { get; init; } = TapeAlertLevel.Information;
    public string Message { get; init; } = "";
    public string Cause { get; init; } = "";
}

public class TapeAlert
{
    public static readonly List<TapeAlertItem> Items = new()
    {
        new()
        {
            BitPosition = 1,
            Name = "Read warning",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The tape drive is having problems reading data. No data has been lost,but there has been a reduction in the capacity of the tape.",
            Cause = "The drive is having severe trouble reading."
        },
        new()
        {
            BitPosition = 2,
            Name = "Write warning",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The tape drive is having problems writing data. No data has been lost,but there has been a reduction in the capacity of the tape.",
            Cause = "The drive is having severe trouble writing."
        },
        new()
        {
            BitPosition = 3,
            Name = "Hard Error",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The operation has stopped because an error has occurred while reading or writing data which the drive cannot correct.",
            Cause = "This flag is set for any unrecoverable read / write/positioning error, and is cleared internally when the tape is ejected.The flagis set as an explanation of the error in con-junction with one of the recovery actionflags 4, 5, or 6."
        },
        new()
        {
            BitPosition = 4,
            Name = "Media",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "Your data is at risk:\n1. Copy any data you require from this tape.\n2. Restart the operation with a different tape.",
            Cause = "Media performance is severely degraded or the tape can no longer be written or read. This flag is set for any unrecoverable read / write/ positioning error caused by faulty media. lt is cleared internally when the media is ejected."
        },
        new()
        {
            BitPosition = 5,
            Name = "Read failure",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The tape is damaged or the drive is faulty. Call the tape supplier's helpline.",
            Cause = "The drive can no longer read data from the tape. The flag is set for any unrecoverable read error where the diagnosis is uncertain and could either be a faulty tape or faulty drive hardware. It is cleared internally when the tape is ejected."
        },
        new()
        {
            BitPosition = 6,
            Name = "Write failure",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The tape is from a faulty batch or the tape drive is faulty:\n1. Use a good tape to test the drive.\n2. If the problem persists, call the tape drive supplier's helpline.",
            Cause = "The drive can no longer write data to the tape. The flag is set for any unrecoverable write/positioning error where the diagnosis is uncertain and could either be a faulty tape or faulty drive hardware. It is cleared internally when the tape is ejected."
        },
        new()
        {
            BitPosition = 7,
            Name = "Media life",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The tape cartridge has reached the end of its calculated useful life:\n1. Copy any data you need to another tape.\n2. Discard the old tape.",
            Cause = "The media has exceeded its specified life."
        },
        new()
        {
            BitPosition = 8,
            Name = "Not data grade",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "Not relevant to Ultrium drives.",
            Cause = ""
        },
        new()
        {
            BitPosition = 9,
            Name = "Write-protect",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "You are trying to write to a write-protected cartridge. Remove the write-protection or use another tape.",
            Cause = "A write command was attempted to a write-protected tape."
        },
        new()
        {
            BitPosition = 10,
            Name = "No removal",
            AlertLevel = TapeAlertLevel.Information,
            Message = "You cannot eject the cartridge because the tape drive is in use. Wait until the operation is complete before ejecting the cartridge.",
            Cause = "A manual or software unload was attempted when Prevent Medium Removal was in force."
        },
        new()
        {
            BitPosition = 11,
            Name = "Cleaning media",
            AlertLevel = TapeAlertLevel.Information,
            Message = "The tape in the drive is a cleaning cartridge.",
            Cause = "A cleaning cartridge is loaded in the drive."
        },
        new()
        {
            BitPosition = 12,
            Name = "Unsupported format",
            AlertLevel = TapeAlertLevel.Information,
            Message = "You have tried to load a cartridge of a type that is not supported by this drive.",
            Cause = "Attempted load of an unsupported tape format."
        },
        new()
        {
            BitPosition = 13,
            Name = "Recoverable mechanical cartridge failure",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The operation has failed because the tape in the drive has experienced a mechanical failure:\n1. Discard the old tape.\n2. Restart the operation with a different tape.",
            Cause = "The tape has snapped or suffered some other mechanical failure in the drive, but the tape can still be ejected."
        },
        new()
        {
            BitPosition = 14,
            Name = "Unrecoverable mechanical cartridge failure",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The operation has failed because the tape in the drive has experienced a mechanical failure:\n1. Do not attempt to extract the tape cartridge.\n2. Call the tape drive supplier's helpline.",
            Cause = "The tape has snapped or suffered some other mechanical failure in the drive and the tape cannot be ejected."
        },
        new()
        {
            BitPosition = 15,
            Name = "Memory chip in cartridge failure",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The memory in the tape cartridge has failed, which reduces performance. Do not use the cartridge for further write operations.",
            Cause = "The LTO-CM chip has failed in cartridge."
        },
        new()
        {
            BitPosition = 16,
            Name = "Forced eject",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The operation has failed because the tape cartridge was manually de-mounted while the tape drive was actively writing or reading.",
            Cause = "A manual or forced eject occurred while the drive was writing or reading."
        },
        new()
        {
            BitPosition = 17,
            Name = "Read-only format",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "You have loaded a cartridge of a type that is read-only in this drive. The cartridge will appear as write-protected.",
            Cause = "A write command has been attempted to a tape whose format is read-only in this drive."
        },
        new()
        {
            BitPosition = 18,
            Name = "Tape directory corrupted on load",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The tape directory on the cartridge has been corrupted. File search performance will be degraded. The tape directory can be rebuilt by reading all the data on the cartridge.",
            Cause = "The drive was powered down with a tape loaded, or a permanent error prevented the tape directory being updated."
        },
        new()
        {
            BitPosition = 19,
            Name = "Nearing media life",
            AlertLevel = TapeAlertLevel.Information,
            Message = "The tape cartridge is nearing the end of its calculated life. It is recommended that you:\n1. Use another tape cartridge for your next backup.\n2. Store this tape cartridge in a safe place in case you need to restore data from it.",
            Cause = "The tape may have exceeded its specified number of passes."
        },
        new()
        {
            BitPosition = 20,
            Name = "Clean now",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The tape drive needs cleaning:\n- If the operation has stopped, eject the tape and clean the drive.\n- If the operation has not stopped, wait for it to finish and then clean the drive.\n- Check the tape drive user's manual for cleaning instructions.",
            Cause = "The tape drive has detected that it needs cleaning. The flag is cleared internally when the drive is cleaned successfully."
        },
        new()
        {
            BitPosition = 21,
            Name = "Clean periodic",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The tape drive is due for routine cleaning:\n1. Wait for the current operation to finish.\n2. Use a cleaning cartridge.\n3. Check the tape drive user's manual for cleaning instructions.",
            Cause = "The drive is ready for a periodic cleaning."
        },
        new()
        {
            BitPosition = 22,
            Name = "Expired cleaning media",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The last cleaning cartridge used in the tape drive has worn out:\n1. Discard the worn-out cleaning cartridge.\n2. Wait for the current operation to finish.\n3. Use a new cleaning cartridge.",
            Cause = "The cleaning tape has expired. The flag is set when the tape drive detects a cleaning cycle was attempted but was not successful. It is cleared internally when the next cleaning cycle is attempted."
        },
        new()
        {
            BitPosition = 23,
            Name = "Invalid cleaning cartridge",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The last cleaning cartridge used in the tape drive was an invalid type:\n1. Do not use this cleaning cartridge in this drive.\n2. Wait for the current operation to finish.\n3. Use a valid cleaning cartridge.",
            Cause = "An invalid cleaning tape type was used."
        },
        new()
        {
            BitPosition = 24,
            Name = "Retension requested",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The tape drive has requested a retension operation.",
            Cause = "The drive is having trouble reading or writing that will be resolved by a retension cycle."
        },
        new()
        {
            BitPosition = 25,
            Name = "Dual-port interface error",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "A redundant interface port on the tape drive has failed.",
            Cause = "One of the interface ports in a dual-port configuration (in other words, Fibre Channel) has failed."
        },
        new()
        {
            BitPosition = 26,
            Name = "Cooling fan failure",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "A tape drive cooling fan has failed.",
            Cause = "A fan inside the drive mechanism or enclosure has failed."
        },
        new()
        {
            BitPosition = 27,
            Name = "Power supply failure",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "A redundant power supply has failed inside the tape drive enclosure. Check the enclosure user's manual for instructions on replacing the failed power supply.",
            Cause = "A redundant PSU has failed inside the tape drive enclosure or rack subsystem."
        },
        new()
        {
            BitPosition = 28,
            Name = "Power consumption",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The tape drive power consumption is outside the specified range.",
            Cause = "The tape drive power consumption is out-side the specified range."
        },
        new()
        {
            BitPosition = 29,
            Name = "Drive maintenance",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "Preventive maintenance of the tape drive is re-quired.Check the tape drive user's manual for preventive maintenance tasks or call the tape drive supplier's helpline.",
            Cause = "The drive requires preventive maintenance (not cleaning)."
        },
        new()
        {
            BitPosition = 30,
            Name = "Hardware A",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The tape drive has a hardware fault:\n1. Eject the tape or magazine.\n2. Reset the drive.\n3. Restart the operation.",
            Cause = "The drive has a hardware fault from which it can recover through a reset."
        },
        new()
        {
            BitPosition = 31,
            Name = "Hardware B",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The tape drive has a hardware fault:\n1. Turn the tape drive off and then on again.\n2. Restart the operation.\n3. If the problem persists, call the tape drive supplier's helpline.",
            Cause = "The drive has a hardware fault that is not read/write related or that it can recover from through a power cycle.The flag is set when the tape drive fails its internal power-on self-tests. It is not cleared internally until the drive is powered off."
        },
        new()
        {
            BitPosition = 32,
            Name = "Interface",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The tape drive has a problem with the application client interface:\n1. Check the cables and cable connections.\n2. Restart the operation.",
            Cause = "The drive has identified an interface fault."
        },
        new()
        {
            BitPosition = 33,
            Name = "Eject media",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The operation has failed:\n1. Eject the tape or magazine.\n2. Insert the tape or magazine again.\n3. Restart the operation.",
            Cause = "Error recovery action."
        },
        new()
        {
            BitPosition = 34,
            Name = "Download fail",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The firmware download has failed because you have tried to use the incorrect firmware for this tape drive.Obtain the correct firmware and try again.",
            Cause = "Firmware download failed."
        },
        new()
        {
            BitPosition = 35,
            Name = "Drive humidity",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "Environmental conditions inside the tape drive are outside the specified humidity range.",
            Cause = "The drive's humidity limits have been exceeded."
        },
        new()
        {
            BitPosition = 36,
            Name = "Drive temperature",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "Environmental conditions inside the tape drive are outside the specified temperature range.",
            Cause = "The drive is experiencing a cooling problem."
        },
        new()
        {
            BitPosition = 37,
            Name = "Drive voltage",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The voltage supply to the tape drive is outside the specified range.",
            Cause = "Drive voltage limits have been exceeded."
        },
        new()
        {
            BitPosition = 38,
            Name = "Predictive failure",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "A hardware failure of the drive is predicted. Call the tape drive supplier's helpline.",
            Cause = "Failure of the drive's hardware is predicted."
        },
        new()
        {
            BitPosition = 39,
            Name = "Diagnostics required",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The tape drive may have a hardware fault. Run extended diagnostics to verify and diagnose the problem. Check the tape drive user's manual for instructions on running extended diagnostic tests.",
            Cause = "The drive may have a hardware fault that may be identified by extended diagnostics (using a SEND DIAGNOSTIC command)."
        },
        new()
        {
            BitPosition = 50,
            Name = "Lost statistics",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "Media statistics have been lost at some time in the past.",
            Cause = "The drive or library has been powered on with a tape loaded."
        },
        new()
        {
            BitPosition = 51,
            Name = "Tape directory invalid at unload",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The tape directory on the tape cartridge just unloaded has been corrupted. File search performance will be degraded. The tape directory can be rebuilt by reading all the data.",
            Cause = "An error has occurred preventing the tape directory being updated on unload."
        },
        new()
        {
            BitPosition = 52,
            Name = "Tape system area write failure",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The tape just unloaded could not write its system area successfully: 1. Copy the data to another tape cartridge. 2. Discard the old cartridge.",
            Cause = "Write errors occurred while writing the system area on unload."
        },
        new()
        {
            BitPosition = 53,
            Name = "Tape system area read failure",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The tape system area could not be read successfully at load time. Copy the data to another tape cartridge.",
            Cause = "Read errors occurred while reading the system area on load."
        },
        new()
        {
            BitPosition = 54,
            Name = "No start of data",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The start of data could not be found on the tape: 1. Check that you are using the correct format tape. 2. Discard the tape or return the tape to your supplier.",
            Cause = "The tape has been damaged, bulk erased, or is of an incorrect format."
        },
        new()
        {
            BitPosition = 55,
            Name = "Loading failure",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The operation has failed because the media cannot be loaded and threaded. 1. Remove the cartridge, inspect it as specified in the product manual, and retry the operation. 2. If the problem persists, call the tape drive supplier's help line.",
            Cause = "The drive is unable to load the cassette and thread the tape."
        },
        new()
        {
            BitPosition = 56,
            Name = "Unrecoverable load failure",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The operation has failed because the tape can not be unloaded: 1. Do not attempt to extract the tape cartridge. 2. Call the tape drive supplier's help line.",
            Cause = "The drive is unable to unload the tape."
        },
        new()
        {
            BitPosition = 57,
            Name = "Automation interface failure",
            AlertLevel = TapeAlertLevel.Critical,
            Message = "The tape drive has a problem with the automation interface: 1. Check the power to the automation system. 2. Check the cables and cable connections. 3. Call the supplier's helpline if the problem persists.",
            Cause = "The drive has identified a fault in the automation interface."
        },
        new()
        {
            BitPosition = 58,
            Name = "Firmware failure",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The tape drive has reset itself due to a detected firmware fault. If the problem persists, call the supplier's helpline.",
            Cause = "There is a firmware bug."
        },
        new()
        {
            BitPosition = 59,
            Name = "WORM medium—integrity check failed",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "The tape drive has detected an inconsistency while checking the WORM tape for integrity. Someone may have tampered with the cartridge.",
            Cause = "Someone has tampered with the WORM tape."
        },
        new()
        {
            BitPosition = 60,
            Name = "WORM medium—overwrite attempted",
            AlertLevel = TapeAlertLevel.Warning,
            Message = "An attempt has been made to overwrite user data on a WORM tape: 1.If you used a WORM tape inadvertently, replace it with a normal data tape. 2.If you used a WORM tape intentionally, check that:• the software application is compatible with the WORM tape format you are using.• the cartridge is bar-coded correctly for WORM.",
            Cause = "The application software does not recognize the tape as WORM."
        }
    };
}
