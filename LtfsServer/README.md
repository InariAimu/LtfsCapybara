# LtfsServer Minimal API

This project contains a minimal ASP.NET Core REST API example.


Run locally:

```bash
dotnet run --project LtfsServer
```

Or from the project directory:

```bash
cd LtfsServer
dotnet run
```

Configuration:

- Defaults are in `appsettings.json` (`Api:Host`, `Api:Port`, `Api:Scheme`).
- Override with environment variables (e.g. `PORT`) or CLI args.

Examples:

- Run on port 8080 via env var:

```bash
PORT=8080 dotnet run --project LtfsServer
```

- Run on a specific host and port via `appsettings.json` or `--urls` override:

```bash
dotnet run --project LtfsServer --urls "http://0.0.0.0:8080"
```

Available endpoints:

- `GET /api/health` — basic health check
- `GET /api/info` — server name and version
- `GET /api/example/files` — sample file list

The root `/` returns a simple status JSON including the configured URL.
