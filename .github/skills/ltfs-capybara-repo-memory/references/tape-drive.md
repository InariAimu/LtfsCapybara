# TapeDrive Repository Memory

- `TapeDriveIncident` plus `TapeDriveCommandException` is the shared low-level incident and escalation path across `TapeDrive` and `Ltfs`.
- Current incident mapping:
  - Win32 errors map to critical incidents that stop all operations.
  - Sense warnings pause current tasks.
  - Sense critical incidents stop all operations.
  - TapeAlert warnings notify only.
  - TapeAlert critical incidents stop all operations.
- `Ltfs` passes a handler through `TapeDriveBase.IncidentHandler`; default behavior is continue for `NotifyOnly` incidents and abort for stronger actions if no custom handler is supplied.
- `TapeDrive.Utils.StructParser` respects `DWordAttribute.Length`; DWord fields are not always 4 bytes.
- Variable-length DWord fields are big-endian and map to the low-significance bytes of the target numeric value.
- Example: `TapeDrive/SCSICommands/WriteFilemarks.cs` uses `[DWord(2, 3)]` for a 3-byte filemark count.