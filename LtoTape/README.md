# LtoTape

`LtoTape` is the cartridge metadata library for LtfsCapybara. It parses LTO cartridge memory data and exposes structured information about media identity, usage history, partitions, EOD markers, tape health, and wrap layout.

## What This Project Does

- Parses cartridge memory from LCG text dumps (`.cm`) and binary dumps.
- Extracts manufacturer, media vendor, generation, serial, and capacity-related metadata.
- Decodes usage pages, partition information, EOD records, tape status, and wrap information.
- Provides common helpers such as big-endian conversion and MAM attribute models.

## Main Types

- `CartridgeMemory`: main parser and aggregate model.
- `PartitionInfo`: partition-specific cartridge memory details.
- `MAMAttribute`: typed access to MAM page data.
- `BigEndianBitConverter`: helper for tape-oriented binary parsing.

## Typical Usage

This library is used by:

- `TapeDrive` to interpret cartridge memory data read from hardware.
- `LtfsServer` when serving locally cached cartridge memory summaries and details.

## Supported Inputs

- Binary cartridge memory files.
- LCG-style text exports that contain a `CM RAW DATA` section.

## Build

```bash
dotnet build LtoTape/LtoTape.csproj
```

## Notes

- Targets `net8.0`.
- The focus here is metadata parsing, not tape I/O. For device communication, use `TapeDrive`.