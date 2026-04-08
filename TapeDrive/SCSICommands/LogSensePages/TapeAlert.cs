

namespace TapeDrive.SCSICommands.LogSensePages;

public enum TapeAlertLevel
{
    Information, Warning, Critical
}

public class TapeAlertItem(string flag, TapeAlertLevel type, string recommendedHostMessage, string cause)
{
    public string Flag { get; init; } = flag;
    public TapeAlertLevel Type { get; init; } = type;
    public string RecommendedHostMessage { get; init; } = recommendedHostMessage;
    public string Cause { get; init; } = cause;
}

public class TapeAlert
{
    public static List<int> ParseMostSignificantBytesIntoIndexes(byte[] bytes)
    {
        var indexes = new List<int>();

        for (int i = 0; i < bytes.Length; i++)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                if ((bytes[i] & (1 << (7 - bit))) != 0)
                {
                    indexes.Add(i * 8 + bit + 1);
                }
            }
        }

        return indexes;
    }
    
    public static readonly Dictionary<int, TapeAlertItem> Items = new()
    {
        [1] = new(
            "Read warning", 
            TapeAlertLevel.Warning, 
            "The tape drive is having problems reading data. No data has been lost,but there has been a reduction in the capacity of the tape.", 
            "The drive is having severe trouble reading."
        ),
        [2] = new(
            "Write warning",
            TapeAlertLevel.Warning,
            "The tape drive is having problems writing data. No data has been lost,but there has been a reduction in the capacity of the tape.",
            "The drive is having severe trouble writing."
        ),
        [3] = new(
            "Hard Error",
            TapeAlertLevel.Warning,
            "The operation has stopped because an error has occurred while reading or writing data which the drive cannot correct.",
            "This flag is set for any unrecoverable read / write/positioning error, and is cleared internally when the tape is ejected.The flagis set as an explanation of the error in con-junction with one of the recovery actionflags 4, 5, or 6."
        ),
        [4] = new(
            "Media",
            TapeAlertLevel.Critical,
            "Your data is at risk:\n1. Copy any data you require from this tape.\n2. Restart the operation with a different tape.",
            "Media performance is severely degraded or the tape can no longer be written or read. This flag is set for any unrecoverable read / write/ positioning error caused by faulty media. lt is cleared internally when the media is ejected."
        ),
        [5] = new(
            "Read failure",
            TapeAlertLevel.Critical,
            "The tape is damaged or the drive is faulty. Call the tape supplier's helpline.",
            "The drive can no longer read data from the tape. The flag is set for any unrecoverable read error where the diagnosis is uncertain and could either be a faulty tape or faulty drive hardware. It is cleared internally when the tape is ejected."
        ),
        [6] = new(
            "Write failure",
            TapeAlertLevel.Critical,
            "The tape is from a faulty batch or the tape drive is faulty:\n1. Use a good tape to test the drive.\n2. If the problem persists, call the tape drive supplier's helpline.",
            "The drive can no longer write data to the tape. The flag is set for any unrecoverable write/positioning error where the diagnosis is uncertain and could either be a faulty tape or faulty drive hardware. It is cleared internally when the tape is ejected."
        ),
        [7] = new(
            "Media life",
            TapeAlertLevel.Warning,
            "The tape cartridge has reached the end of its calculated useful life:\n1. Copy any data you need to another tape.\n2. Discard the old tape.",
            "The media has exceeded its specified life."
        ),
        [8] = new(
            "Not data grade",
            TapeAlertLevel.Warning,
            "Not relevant to Ultrium drives.",
            ""
        ),
        [9] = new(
            "Write-protect",
            TapeAlertLevel.Critical,
            "You are trying to write to a write-protected cartridge. Remove the write-protection or use another tape.",
            "A write command was attempted to a write-protected tape."
        ),
        [10] = new(
            "No removal",
            TapeAlertLevel.Information,
            "You cannot eject the cartridge because the tape drive is in use. Wait until the operation is complete before ejecting the cartridge.",
            "A manual or software unload was attempted when Prevent Medium Removal was in force."
        ),
        [11] = new(
            "Cleaning media",
            TapeAlertLevel.Information,
            "The tape in the drive is a cleaning cartridge.",
            "A cleaning cartridge is loaded in the drive."
        ),
        [12] = new(
            "Unsupported format",
            TapeAlertLevel.Information,
            "You have tried to load a cartridge of a type that is not supported by this drive.",
            "Attempted load of an unsupported tape format."
        ),
        [13] = new(
            "Recoverable mechanical cartridge failure",
            TapeAlertLevel.Critical,
            "The operation has failed because the tape in the drive has experienced a mechanical failure:\n1. Discard the old tape.\n2. Restart the operation with a different tape.",
            "The tape has snapped or suffered some other mechanical failure in the drive, but the tape can still be ejected."
        ),
        [14] = new(
            "Unrecoverable mechanical cartridge failure",
            TapeAlertLevel.Critical,
            "The operation has failed because the tape in the drive has experienced a mechanical failure:\n1. Do not attempt to extract the tape cartridge.\n2. Call the tape drive supplier's helpline.",
            "The tape has snapped or suffered some other mechanical failure in the drive and the tape cannot be ejected."
        ),
        [15] = new(
            "Memory chip in cartridge failure",
            TapeAlertLevel.Warning,
            "The memory in the tape cartridge has failed, which reduces performance. Do not use the cartridge for further write operations.",
            "The LTO-CM chip has failed in cartridge."
        ),
        [16] = new(
            "Forced eject",
            TapeAlertLevel.Critical,
            "The operation has failed because the tape cartridge was manually de-mounted while the tape drive was actively writing or reading.",
            "A manual or forced eject occurred while the drive was writing or reading."
        ),
        [17] = new(
            "Read-only format",
            TapeAlertLevel.Critical,
            "You have loaded a cartridge of a type that is read-only in this drive. The cartridge will appear as write-protected.",
            "A write command has been attempted to a tape whose format is read-only in this drive."
        ),
        [18] = new(
            "Tape directory corrupted on load",
            TapeAlertLevel.Warning,
            "The tape directory on the cartridge has been corrupted. File search performance will be degraded. The tape directory can be rebuilt by reading all the data on the cartridge.",
            "The drive was powered down with a tape loaded, or a permanent error prevented the tape directory being updated."
        ),
        [19] = new(
            "Nearing media life",
            TapeAlertLevel.Information,
            "The tape cartridge is nearing the end of its calculated life. It is recommended that you:\n1. Use another tape cartridge for your next backup.\n2. Store this tape cartridge in a safe place in case you need to restore data from it.",
            "The tape may have exceeded its specified number of passes."
        ),
        [20] = new(
            "Clean now",
            TapeAlertLevel.Critical,
            "The tape drive needs cleaning:\n- If the operation has stopped, eject the tape and clean the drive.\n- If the operation has not stopped, wait for it to finish and then clean the drive.\n- Check the tape drive user's manual for cleaning instructions.",
            "The tape drive has detected that it needs cleaning. The flag is cleared internally when the drive is cleaned successfully."
        ),
        [21] = new(
            "Clean periodic",
            TapeAlertLevel.Warning,
            "The tape drive is due for routine cleaning:\n1. Wait for the current operation to finish.\n2. Use a cleaning cartridge.\n3. Check the tape drive user's manual for cleaning instructions.",
            "The drive is ready for a periodic cleaning."
        ),
        [22] = new(
            "Expired cleaning media",
            TapeAlertLevel.Critical,
            "The last cleaning cartridge used in the tape drive has worn out:\n1. Discard the worn-out cleaning cartridge.\n2. Wait for the current operation to finish.\n3. Use a new cleaning cartridge.",
            "The cleaning tape has expired. The flag is set when the tape drive detects a cleaning cycle was attempted but was not successful. It is cleared internally when the next cleaning cycle is attempted."
        ),
        [23] = new(
            "Invalid cleaning cartridge",
            TapeAlertLevel.Critical,
            "The last cleaning cartridge used in the tape drive was an invalid type:\n1. Do not use this cleaning cartridge in this drive.\n2. Wait for the current operation to finish.\n3. Use a valid cleaning cartridge.",
            "An invalid cleaning tape type was used."
        ),
        [24] = new(
            "Retension requested",
            TapeAlertLevel.Warning,
            "The tape drive has requested a retension operation.",
            "The drive is having trouble reading or writing that will be resolved by a retension cycle."
        ),
        [25] = new(
            "Dual-port interface error",
            TapeAlertLevel.Warning,
            "A redundant interface port on the tape drive has failed.",
            "One of the interface ports in a dual-port configuration (in other words, Fibre Channel) has failed."
        ),
        [26] = new(
            "Cooling fan failure",
            TapeAlertLevel.Warning,
            "A tape drive cooling fan has failed.",
            "A fan inside the drive mechanism or enclosure has failed."
        ),
        [27] = new(
            "Power supply failure",
            TapeAlertLevel.Warning,
            "A redundant power supply has failed inside the tape drive enclosure. Check the enclosure user's manual for instructions on replacing the failed power supply.",
            "A redundant PSU has failed inside the tape drive enclosure or rack subsystem."
        ),
        [28] = new(
            "Power consumption",
            TapeAlertLevel.Warning,
            "The tape drive power consumption is outside the specified range.",
            "The tape drive power consumption is out-side the specified range."
        ),
        [29] = new(
            "Drive maintenance",
            TapeAlertLevel.Warning,
            "Preventive maintenance of the tape drive is re-quired.Check the tape drive user's manual for preventive maintenance tasks or call the tape drive supplier's helpline.",
            "The drive requires preventive maintenance (not cleaning)."
        ),
        [30] = new(
            "Hardware A",
            TapeAlertLevel.Critical,
            "The tape drive has a hardware fault:\n1. Eject the tape or magazine.\n2. Reset the drive.\n3. Restart the operation.",
            "The drive has a hardware fault from which it can recover through a reset."
        ),
        [31] = new(
            "Hardware B",
            TapeAlertLevel.Critical,
            "The tape drive has a hardware fault:\n1. Turn the tape drive off and then on again.\n2. Restart the operation.\n3. If the problem persists, call the tape drive supplier's helpline.",
            "The drive has a hardware fault that is not read/write related or that it can recover from through a power cycle.The flag is set when the tape drive fails its internal power-on self-tests. It is not cleared internally until the drive is powered off."
        ),
        [32] = new(
            "Interface",
            TapeAlertLevel.Warning,
            "The tape drive has a problem with the application client interface:\n1. Check the cables and cable connections.\n2. Restart the operation.",
            "The drive has identified an interface fault."
        ),
        [33] = new(
            "Eject media",
            TapeAlertLevel.Critical,
            "The operation has failed:\n1. Eject the tape or magazine.\n2. Insert the tape or magazine again.\n3. Restart the operation.",
            "Error recovery action."
        ),
        [34] = new(
            "Download fail",
            TapeAlertLevel.Warning,
            "The firmware download has failed because you have tried to use the incorrect firmware for this tape drive.Obtain the correct firmware and try again.",
            "Firmware download failed."
        ),
        [35] = new(
            "Drive humidity",
            TapeAlertLevel.Warning,
            "Environmental conditions inside the tape drive are outside the specified humidity range.",
            "The drive's humidity limits have been exceeded."
        ),
        [36] = new(
            "Drive temperature",
            TapeAlertLevel.Warning,
            "Environmental conditions inside the tape drive are outside the specified temperature range.",
            "The drive is experiencing a cooling problem."
        ),
        [37] = new(
            "Drive voltage",
            TapeAlertLevel.Warning,
            "The voltage supply to the tape drive is outside the specified range.",
            "Drive voltage limits have been exceeded."
        ),
        [38] = new(
            "Predictive failure",
            TapeAlertLevel.Critical,
            "A hardware failure of the drive is predicted. Call the tape drive supplier's helpline.",
            "Failure of the drive's hardware is predicted."
        ),
        [39] = new(
            "Diagnostics required",
            TapeAlertLevel.Warning,
            "The tape drive may have a hardware fault. Run extended diagnostics to verify and diagnose the problem. Check the tape drive user's manual for instructions on running extended diagnostic tests.",
            "The drive may have a hardware fault that may be identified by extended diagnostics (using a SEND DIAGNOSTIC command)."
        ),
        [50] = new(
            "Lost statistics",
            TapeAlertLevel.Warning,
            "Media statistics have been lost at some time in the past.",
            "The drive or library has been powered on with a tape loaded."
        ),
        [51] = new(
            "Tape directory invalid at unload",
            TapeAlertLevel.Warning,
            "The tape directory on the tape cartridge just unloaded has been corrupted. File search performance will be degraded. The tape directory can be rebuilt by reading all the data.",
            "An error has occurred preventing the tape directory being updated on unload."
        ),
        [52] = new(
            "Tape system area write failure",
            TapeAlertLevel.Critical,
            "The tape just unloaded could not write its system area successfully: 1. Copy the data to another tape cartridge. 2. Discard the old cartridge.",
            "Write errors occurred while writing the system area on unload."
        ),
        [53] = new(
            "Tape system area read failure",
            TapeAlertLevel.Critical,
            "The tape system area could not be read successfully at load time. Copy the data to another tape cartridge.",
            "Read errors occurred while reading the system area on load."
        ),
        [54] = new(
            "No start of data",
            TapeAlertLevel.Critical,
            "The start of data could not be found on the tape: 1. Check that you are using the correct format tape. 2. Discard the tape or return the tape to your supplier.",
            "The tape has been damaged, bulk erased, or is of an incorrect format."
        ),
        [55] = new(
            "Loading failure",
            TapeAlertLevel.Critical,
            "The operation has failed because the media cannot be loaded and threaded. 1. Remove the cartridge, inspect it as specified in the product manual, and retry the operation. 2. If the problem persists, call the tape drive supplier's help line.",
            "The drive is unable to load the cassette and thread the tape."
        ),
        [56] = new(
            "Unrecoverable load failure",
            TapeAlertLevel.Critical,
            "The operation has failed because the tape can not be unloaded: 1. Do not attempt to extract the tape cartridge. 2. Call the tape drive supplier's help line.",
            "The drive is unable to unload the tape."
        ),
        [57] = new(
            "Automation interface failure",
            TapeAlertLevel.Critical,
            "The tape drive has a problem with the automation interface: 1. Check the power to the automation system. 2. Check the cables and cable connections. 3. Call the supplier's helpline if the problem persists.",
            "The drive has identified a fault in the automation interface."
        ),
        [58] = new(
            "Firmware failure",
            TapeAlertLevel.Warning,
            "The tape drive has reset itself due to a detected firmware fault. If the problem persists, call the supplier's helpline.",
            "There is a firmware bug."
        ),
        [59] = new(
            "WORM medium—integrity check failed",
            TapeAlertLevel.Warning,
            "The tape drive has detected an inconsistency while checking the WORM tape for integrity. Someone may have tampered with the cartridge.",
            "Someone has tampered with the WORM tape."
        ),
        [60] = new(
            "WORM medium—overwrite attempted",
            TapeAlertLevel.Warning,
            "An attempt has been made to overwrite user data on a WORM tape: 1.If you used a WORM tape inadvertently, replace it with a normal data tape. 2.If you used a WORM tape intentionally, check that:• the software application is compatible with the WORM tape format you are using.• the cartridge is bar-coded correctly for WORM.",
            "The application software does not recognize the tape as WORM."
        )
    };
}
