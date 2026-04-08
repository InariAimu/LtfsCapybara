# LtfsServer

`LtfsServer` is the ASP.NET Core backend for LtfsCapybara. It hosts the REST API used by the GUI and coordinates tape drive discovery, local tape metadata, LTFS index browsing, task groups, settings, and AI-assisted workflows.

## What This Project Does

- Hosts the HTTP API for the desktop/web frontend.
- Registers and exposes tape drive operations through `ITapeDriveService`.
- Scans locally cached cartridge memory and LTFS index files through `ILocalTapeRegistry`.
- Serves LTFS directory data from cached local indexes.
- Persists and edits server-side settings.
- Stores and edits tape-scoped task groups.
- Proxies AI chat/tool requests through server endpoints.

## Main Endpoint Areas

- `/api/tapedrives`: tape drive discovery and machine actions.
- `/api/localtapes`: cached tape summary list.
- `/api/local/{tapeName}`: LTFS index browsing from local cache.
- `/api/localcm/{tapeName}`: parsed cartridge memory details.
- `/api/tasks/groups`: tape task group management.
- `/api/ai/tools` and `/api/ai/chat/completions`: AI tool discovery and chat proxy.
- `/api/health` and `/api/info`: service metadata and health.

## Configuration

Defaults come from `appsettings.json`:

- `Api:Host`, `Api:Port`, `Api:Scheme`
- `Data:Path`
- `ServerSettings:*`
- `TapeDrive:UseFakeDrive`
- `AI:model`

At startup, the server resolves `Data:Path` as follows:

- Uses `Data:Path` from configuration when provided.
- Otherwise defaults to `Documents/LtfsCapybara`.
- Loads optional runtime overrides from `{Data.Path}/config.json`.

## Run

From the repository root:

```bash
dotnet run --project LtfsServer
```

Or from the project directory:

```bash
cd LtfsServer
dotnet run
```

Build only:

```bash
dotnet build LtfsServer/LtfsServer.csproj
```

## Development Notes

- The default server URL is `http://localhost:5003`.
- CORS is enabled for the Vite development origin `http://localhost:1420`.
- The project references `Ltfs`, `LtoTape`, and `TapeDrive` directly.

## Notes

- This is not a generic sample API; it is the repository's integration point between the UI and the tape/LTFS libraries.
- The feature folders under `Features/` map closely to API surface areas and service boundaries.
