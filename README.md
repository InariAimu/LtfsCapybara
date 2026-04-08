<div align="center">

<img width="200" src="logo.png">

# LtfsCapybara

[![capybara](https://img.shields.io/badge/Ltfs-Capybara-brown)](#)
[![License](https://img.shields.io/static/v1?label=LICENSE&message=GNU%20GPLv3&color=lightrey)](./blob/main/LICENSE)

</div>

LtfsCapybara is a Windows-focused LTFS toolkit for managing LTO tape media. The repository includes the core LTFS libraries, a tape-drive hardware access layer, cartridge-memory parsing, an ASP.NET Core API server, and a Vue + Tauri desktop GUI.

## Overview

Core capabilities currently implemented include:

- LTFS 2.4 labels, index handling, and tape formatting flows.
- Tape load, unload, unthread, rewind, read, write, and positioning operations.
- Cartridge memory parsing for `.cm` and binary dumps.
- Local cache and inspection of LTFS indexes and tape metadata.
- REST APIs for tape drives, local tapes, local indexes, settings, tasks, and AI-assisted operations.
- Desktop UI for browsing tapes, task groups, tape-machine actions, and local index content.

The codebase is organized around this stack:

```text
GUI (Vue 3 + Tauri)
	|
	v
LtfsServer (ASP.NET Core API)
	|
	v
Ltfs (LTFS logic)
	|
	v
TapeDrive (Windows tape I/O + SCSI)
	|
	v
LtoTape (cartridge metadata parsing)
```

## Repository Layout

- [Ltfs/README.md](Ltfs/README.md): core LTFS library for format, read, write, verify, labels, and index operations.
- [TapeDrive/README.md](TapeDrive/README.md): Windows tape hardware access layer built on tape device handles and SCSI commands.
- [LtoTape/README.md](LtoTape/README.md): cartridge memory and metadata parsing library.
- [LtfsServer/README.md](LtfsServer/README.md): ASP.NET Core backend used by the GUI and tooling.
- [ltfs-capybara-gui/README.md](ltfs-capybara-gui/README.md): Vue 3 + Tauri frontend.
- `LtfsTest/`: automated tests for LTFS labels, indexes, MAM, verification, and update behavior.
- `Test/`: reference console project showing how to use the libraries directly.
- `TestLocal/`: local resource and sample-data testing helpers.
- `Tools/barcodegen/`: barcode generation utility.
- `Tools/ltobox/`: RP2040-based display tool for tape information.
- `protocol/apidoc.md`: API notes for the current `LtfsServer` surface.

## Requirements

- .NET 8 SDK
- Node.js
- pnpm for GUI development
- Rust toolchain and Tauri prerequisites for desktop GUI builds
- Windows for direct tape-drive access

## Getting Started

Build the .NET solution:

```bash
dotnet build LtfsCapybara.sln
```

Run the tests:

```bash
dotnet test LtfsCapybara.sln
```

Run the API server:

```bash
dotnet run --project LtfsServer
```

The default server URL is `http://localhost:5003`.

Run the GUI in development mode:

```bash
pnpm -C ltfs-capybara-gui install
pnpm -C ltfs-capybara-gui dev
```

The Vite development server runs on `http://localhost:1420`, and `LtfsServer` is configured to allow that origin via CORS.

## Build Artifacts

For a repository-level release build on Windows, use:

```bat
buildall.bat
```

The script publishes non-test .NET projects and then builds the Tauri GUI, collecting artifacts into the top-level `build` directory.

## Server Surface

`LtfsServer` currently exposes API areas for:

- health and service info
- tape drive discovery and machine actions
- cached local tape summaries
- local LTFS index browsing
- local filesystem browsing
- server settings
- tape-scoped task groups
- AI tool discovery and chat proxy endpoints

See [LtfsServer/README.md](LtfsServer/README.md) and `protocol/apidoc.md` for more detail.

## Development Notes

- Tape hardware access is implemented in `TapeDrive` and is Windows-specific in practice.
- `TapeDriveBase` allows the higher-level LTFS code and tests to run against fake drives.
- The GUI uses Vue 3, Naive UI, Pinia, vue-i18n, and Tauri 2.
- The repository includes both direct library usage examples and server-backed UI flows.

## Reference Projects

If you want to understand usage patterns first, start with:

- `Test/` for direct library usage.
- `LtfsTest/` for expected behavior and regression coverage.
- `LtfsServer/` for the HTTP integration layer.
- `ltfs-capybara-gui/` for the current end-user workflow.

## Disclaimer

This project is under active development and may have deprecated or unstable features. Use at your own risk.
This is NOT a backup solution. Please ensure you have proper backups of your data before using this software.

> This software is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement. In no event shall the authors or copyright holders be liable for any claim, damages or other liability, whether in an action of contract, tort or otherwise, arising from, out of or in connection with the software or the use or other dealings in the software.

## License

Licensed under GNU GPLv3 with ❤.
