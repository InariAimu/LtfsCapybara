---
description: "Use when editing TapeDrive or Ltfs backend code, especially SCSI commands, struct parsing, tape incidents, IO control, or commit/error handling. Covers incident policy, partial-class boundaries, and variable-length DWord parsing."
name: "TapeDrive Backend Rules"
applyTo:  "TapeDrive/**/*.cs, Ltfs/**/*.cs"
---

# TapeDrive Backend Rules

- Preserve the shared incident path built around `TapeDriveIncident`, `TapeDriveIncidentAction`, and `TapeDriveCommandException`; do not introduce parallel low-level error channels unless there is a strong reason.
- Keep current incident policy semantics intact: Win32 and critical SCSI status errors stop all operations; sense warnings pause current tasks; sense critical incidents stop all operations; TapeAlert warnings notify only; critical TapeAlert incidents stop all operations.
- `Ltfs` wires `TapeDriveBase.IncidentHandler`; if no higher-level handler is provided, `NotifyOnly` incidents continue and stronger actions abort. Preserve that default escalation behavior when changing task or commit flows.
- When editing SCSI struct parsing, treat `DWordAttribute.Length` as meaningful. DWord fields can be 1 to 4 bytes, use big-endian order, and occupy the low-significance bytes of the numeric value.
- Known example for variable-width DWord parsing: `TapeDrive/SCSICommands/WriteFilemarks.cs` uses `[DWord(2, 3)]` for a 3-byte filemark count. Follow that pattern instead of assuming a fixed 4-byte integer.
- Prefer targeted edits within the existing partial-file layout such as `TapeDrive.IO.cs`, `TapeDrive.Sense.cs`, and `TapeDrive.IOCtl.cs`.