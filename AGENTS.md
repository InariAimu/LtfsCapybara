# Ltfs Capybara Agent Guide

## Project Structure

- `Ltfs`: LTFS filesystem core in C#, including format, read, write, verify, index operations, and task-backed write flows.
- `TapeDrive`: Windows LTO tape access layer in C#, including SCSI commands, IO, sense handling, alerts, positioning, and MAM access.
- `LtoTape`: Cartridge memory and tape metadata parsing.
- `LtfsServer`: ASP.NET Core minimal API server. Bootstrapping lives in `LtfsServer/BootStrap`; feature endpoints and service code live in `LtfsServer/Features`.
- `ltfs-capybara-gui`: Vue 3 + TypeScript + Tauri desktop GUI using Naive UI.
- `LtfsTest`: Unit tests for LTFS and related behaviors.
- `Test` and `TestLocal`: reference or local execution flows.
- `Tools`: utility subprojects such as barcode generation and hardware helpers.

## Agent Coding Guidance

### General

- Read neighboring partial classes, feature files, and tests before changing behavior.
- Prefer root-cause fixes that preserve streaming behavior, minimize tape movement, and avoid broad refactors.
- Keep changes consistent with existing API and DTO shapes unless the task explicitly requires a contract change.

### C# Backend

- Use `Task` or `Task<T>` for long-running tape operations.
- Depend on `TapeDriveBase` abstractions where possible for testability.
- Validate tape loaded or threaded state before issuing tape IO.
- Handle index and data partitions deliberately; partition switches are expensive.
- Preserve the existing partial-class layout in `TapeDrive` when adding backend code.

### TapeDrive And LTFS

- Treat `TapeDriveIncident` and `TapeDriveCommandException` as the shared incident path between `TapeDrive` and `Ltfs`.
- Preserve current incident escalation semantics unless the task explicitly changes policy.
- When editing SCSI structure parsing, verify field widths carefully; `DWordAttribute.Length` can be shorter than 4 bytes.

### Server And API

- Keep endpoint registration in `MapXxxApi` extension classes under `LtfsServer/Features`.
- Return lean DTOs, records, or anonymous payloads through `Results.Ok(...)`, `Results.BadRequest(...)`, and `Results.NotFound(...)`.
- Register or wire services through `LtfsServer/BootStrap/StartupConfig.cs`.

### GUI

- Run Node and package-manager commands from `ltfs-capybara-gui`.
- Reuse shared frontend path helpers and keep i18n plus Naive UI locale wiring intact.
- Validate GUI changes with `pnpm -C .\\ltfs-capybara-gui build`.
