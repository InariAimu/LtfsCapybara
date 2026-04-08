# ltfs-capybara-gui

`ltfs-capybara-gui` is the desktop frontend for LtfsCapybara. It combines Vue 3, Vite, Naive UI, Pinia, and Tauri to provide an interactive interface for browsing tapes, inspecting LTFS indexes, managing task groups, and controlling tape drives through `LtfsServer`.

## What This Project Does

- Lists discovered tape drives and exposes tape machine actions.
- Displays local tape summaries and cached LTFS index content.
- Browses files and directories from local LTFS index snapshots.
- Manages task groups associated with tapes.
- Includes an AI chat surface backed by server-side AI tool endpoints.
- Supports internationalization and light/dark themes.

## Tech Stack

- Vue 3 + TypeScript
- Vite
- Tauri
- Naive UI
- Pinia
- vue-i18n

## Project Structure

- `src/views/`: top-level screens such as `Main`, `Overview`, `TapeMachine`, `TapeLibrary`, `LocalIndex`, `Task`, `AIChat`, and `Settings`.
- `src/api/`: HTTP client wrappers for backend endpoints.
- `src/stores/`: Pinia state management.
- `src/i18n/`: locale setup and translations.
- `src/theme/`: application theme handling.

## Development

Install dependencies:

```bash
pnpm install
```

Run the web UI in development mode:

```bash
pnpm dev
```

Build the frontend:

```bash
pnpm build
```

Format source files:

```bash
pnpm format
```

Run the Tauri app shell:

```bash
pnpm tauri dev
```

## Backend Integration

- During development, the Vite app runs on `http://localhost:1420`.
- `LtfsServer` enables CORS for that origin.
- The server defaults to `http://localhost:5003` unless overridden by configuration.

## Notes

- The GUI is designed around the repository's tape-management workflow rather than generic LTFS inspection.
- When changing UI code, keep i18n resources and Naive UI locale bindings in sync.
