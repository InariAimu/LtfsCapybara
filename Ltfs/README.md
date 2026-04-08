# Ltfs

`Ltfs` is the core .NET library in LtfsCapybara. It implements the LTFS data model and tape workflow used to format media, read existing tapes, manage LTFS indexes, and write or verify file content on LTO drives.

## What This Project Does

- Implements LTFS 2.4 metadata handling for labels, indexes, MAM attributes, and VCI blocks.
- Formats media by creating index and data partitions, writing LTFS labels, and initializing the first index generations.
- Reads and updates LTFS directory trees through types such as `LtfsIndex`, `LtfsDirectory`, and `LtfsFile`.
- Supports file read, write, verify, and task-driven index updates through the partial `Ltfs` implementation.
- Uses `TapeDriveBase` so logic can run against the real `LTOTapeDrive` or test doubles.

## Main Areas

- `Ltfs.cs`: tape load/unload, format flow, core state.
- `Ltfs.FileReader.cs`: reading LTFS files from tape.
- `Ltfs.FileWriter.cs`: write pipeline and data transfer logic.
- `Ltfs.FileSystem.cs`: path and filesystem-style operations.
- `Ltfs.IndexOperations.cs`: index generation and update helpers.
- `Index/`: XML-backed LTFS index object model.
- `Label/`: LTFS and VOL1 label types.

## Dependencies

- Targets `net8.0`.
- References `TapeDrive` for device access.
- Uses `System.IO.Hashing` for checksum-related work.

## Build

```bash
dotnet build Ltfs/Ltfs.csproj
```

## Where It Is Used

- `LtfsServer` uses this library to expose LTFS data through the API.
- `LtfsTest` covers labels, indexes, MAM handling, verify flows, and index update behavior.
- `Test` contains reference code for working with the library directly.

## Notes

- The library is designed for LTO tape workflows, not general-purpose filesystem storage.
- For implementation examples, start with the `Test` and `LtfsTest` projects in the repository root.