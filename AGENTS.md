# Ltfs Capybara AI Agent Coding Guide

This document provides guidelines for implementing AI agents in the Ltfs Capybara project.

## Overview

LtfsCapybara is a comprehensive LTFS (Linear Tape File System) implementation designed for managing and accessing data stored on LTO (Linear Tape Open) tape drives. It supports LTFS 2.4 standard compliance with high-performance zero-copy pipeline implementation (160 MB/s write speed on LTO-6).

## Subprojects

### Core Libraries

- **`Ltfs`**: LTFS filesystem implementation in C#
  - Format, read, write, and verify tape operations
  - Index partition management (stores tape catalog)
  - Data partition management (stores actual file data)
  - FileSystem operations: `FindFile()`, `ReadFile()`, `UpdateIndexByTask()`
  - Supports multi-threaded operations with `WriteTask` pipeline

- **`TapeDrive`**: LTO tape drive access library in C# (Windows)
  - SCSI command interface for LTO tape devices
  - Tape positioning and movement (`Load()`, `Unload()`, `Rewind()`, `Unthread()`)
  - Read/Write buffer operations
  - Cartridge Memory (CM) and MAM attribute access
  - Partition switching and position tracking
  - Device enumeration and detection

- **`LtoTape`**: LTO tape information library in C#
  - CartridgeMemory (.cm) file parsing (binary and LCG text formats)
  - Cartridge metadata extraction (generation, vendor, capacity, serial numbers)
  - `.cm` file naming convention: `{Barcode}_{Partition}_{Gen}_{Location}_{yyyyMMdd}_{HHmmss}.{fraction}.cm`
  - Storage: tape summaries cached from latest `.cm` files in `AppData.Path/local/`

### Server & API

- **`LtfsServer`**: ASP.NET Core web server in C#
  - RESTful API for remote tape management
  - Tape drive registry (enumerate available drives)
  - Local tape registry (scan tape metadata from cartridge memory files)
  - CORS support for Vite dev server development
  - Endpoints:
    - `GET /api/{specific_api}`

### User Interface

- **`ltfs-capybara-gui`**: Vue.js + Tauri desktop application
  - Cross-platform GUI for tape management
  - Uses NaiveUI component library
  - Internationalization (i18n) support (English, Chinese)
  - Theme support (light/dark)
  - Directory/file browsing of tape contents
  - Tape information and metadata viewing

### Utilities

- **`Tools/barcodegen`**: Python barcode image generator for LTO tape labels
- **`Tools/ltobox`**: C-based RP2040 display box driver for showing LTO tape information

### Test & Sample Projects

- **`Test`**: Reference implementation project (follow for coding patterns)
- **`TestLocal`**: Local resources testing (XML samples, cartridge memory files)
- **`LtfsTest`**: Unit tests for LTFS operations (index, label, MAM, file operations)

## Data Flow

### Architecture Layers

```
User ↔ GUI (ltfs-capybara-gui, Tauri)
         ↓
      LtfsServer (ASP.NET Core REST API)
         ↓
    Ltfs Library (Filesystem operations)
         ↓
    TapeDrive Library (Tape I/O & SCSI commands)
         ↓
    LtoTape Library (Cartridge metadata)
         ↓
    Physical LTO Tape Drive (Hardware)
```

## Coding Guidelines

### C# Backend (Ltfs, TapeDrive, LtoTape, LtfsServer)

- Follow async patterns with `Task<T>` for long operations (Format, Read, Write)
- Use `TapeDriveBase` as interface for test mock support
- Always validate tape loaded state before operations
- Handle both Index Partition and Data Partition for robustness
- SCSI commands encapsulated in `TapeDrive` class
- Use partial classes for code organization (e.g., `TapeDrive.IO.cs`, `TapeDrive.MAM.cs`)
- Register services in `StartupConfig.cs` for dependency injection

### Web API (LtfsServer)

- Use minimal API pattern (MapGet, MapPost extensions)
- Return results with `Results.Ok(data)` or `Results.BadRequest(error)`
- Implement MapXxxApi static methods (one per concern)
- Support CORS for development Vite integration
- Inject `ITapeDriveRegistry` or `ILocalTapeRegistry` into handlers
- Return DTOs (anonymous objects) instead of full domain models
- Configuration in `appsettings.json` and `StartupConfig.cs`

### GUI (ltfs-capybara-gui)

- When modifying the GUI, cd into the `ltfs-capybara-gui` folder
- Use NaiveUI components as much as possible
- Check for i18n compatibility (add text to `i18n` resource files)
- Follow Vue 3 Composition API + TypeScript patterns
- Run `npm run format` to format code before commit
- Store API calls in `api/` service folder
- Use Tauri's `invoke()` for OS-level operations if needed

### Performance Considerations

- **Zero-copy pipeline**: Use streaming for large files, not buffering
- **Partition switching**: Minimize tape position movements
- **Index caching**: Leverage cached `.cm` files in `/api/localtapes` summary
- **Async operations**: Format and bulk read/write should be async (`FormatAsync()`)
- **SCSI optimization**: Batch commands where possible

## Development Workflow

### Running Server
```bash
cd LtfsServer
dotnet run
```
Server runs on configured host/port (default: `http://0.0.0.0:5003`)

### Running GUI
```bash
cd ltfs-capybara-gui
npm install
npm run dev
```
GUI connects to LtfsServer at `http://localhost:{serverPort}`

### Running Tests
```bash
dotnet test
```
Reference implementations in `Test` and `TestLocal` projects

## Key Classes & Interfaces

| Class | Purpose |
|-------|---------|
| `Ltfs` | Main filesystem API (Format, Read, Write, Index management) |
| `LTOTapeDrive` | Tape hardware interface (SCSI commands, positioning) |
| `CartridgeMemory` | Tape metadata parser |
| `LtfsIndex` | File tree structure (XML-based index) |
| `LtfsDirectory`, `LtfsFile` | Index tree nodes |
| `WriteTask` | Tracks pending writes for transaction support |
| `ITapeDriveRegistry` | Enumerates available tape drives |
| `ILocalTapeRegistry` | Manages cached tape metadata |
