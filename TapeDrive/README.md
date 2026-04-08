# TapeDrive

`TapeDrive` is the Windows tape hardware access layer for LtfsCapybara. It wraps low-level Win32 and SCSI operations needed to control LTO drives and move data to and from tape media.

## What This Project Does

- Opens tape devices such as `\\.\Tape0` and communicates with them through SCSI pass-through operations.
- Handles media lifecycle actions such as load, unload, unthread, rewind, reserve, and release.
- Reads and writes blocks, file marks, position data, mode pages, and block limits.
- Supports partition configuration, encryption setup, MAM attribute access, barcode handling, and diagnostic cartridge memory reads.
- Exposes a test-friendly abstraction through `TapeDriveBase` so higher layers can run without hardware.

## Main Areas

- `TapeDrive.Base.cs`: abstract base class used by tests and higher-level libraries.
- `TapeDrive.cs`: main `LTOTapeDrive` implementation and core commands.
- `TapeDrive.IO.cs`: block I/O operations.
- `TapeDrive.MAM.cs`: MAM attribute read/write support.
- `TapeDrive.LogSense.cs` and `TapeDrive.Sense.cs`: sense/log page helpers.
- `SCSICommands/`: command structure definitions.
- `Utils/`: parsing and interop helpers.

## Platform

- Targets `net8.0`.
- Windows-only in practice because it depends on tape device handles and Windows tape/SCSI access patterns.

## Build

```bash
dotnet build TapeDrive/TapeDrive.csproj
```

## Notes

- `LTOTapeDrive` is the real hardware implementation.
- `TapeDriveBase` exists so `Ltfs` and tests can inject fake drives instead of touching physical hardware.
- This project is the lowest-level tape access layer in the repository. Higher-level LTFS behavior lives in `Ltfs`.