# Tasks And API Repository Memory

- Older task-model exploration predated the current `LtfsServer/Features/Tasks` implementation; use those notes as background, not as current truth.
- The current server already exposes task group and task execution APIs under `LtfsServer/Features/Tasks`.
- Existing server conventions still match the earlier exploration in two places:
  - minimal API endpoint registration through `MapXxxApi` extension classes
  - lean DTOs, records, or anonymous payloads serialized by `System.Text.Json`
- Barcode remains a cross-cutting identity source for tape-related flows. Useful sources include `Ltfs.Barcode`, cartridge memory barcode fields, and filename-derived values in local registry code.
- When adding task behavior, prefer extending the current task models and services instead of creating a second task system beside them.