---
description: "Use when editing LtfsServer minimal APIs, task models, task execution flow, DTOs, or barcode-driven tape task grouping. Covers current task feature location, API response style, and safe use of older task-model notes."
name: "Tasks And API Rules"
applyTo: "LtfsServer/**/*.cs"
---

# Tasks And API Rules

- Follow the existing minimal API pattern: keep endpoint registration in `MapXxxApi` extension classes under `LtfsServer/Features`, and return lean DTOs, records, or anonymous payloads through `Results.Ok(...)`, `Results.BadRequest(...)`, or `Results.NotFound(...)`.
- Task execution and task group behavior already live under `LtfsServer/Features/Tasks`. Extend the existing models and services before introducing a new task abstraction.
- When a feature needs a stable tape identity, prefer barcode-centric flows. Barcode information can surface from `Ltfs.Barcode`, cartridge memory application-specific barcode fields, or filename-derived local registry data.
- Older task-model exploration notes are useful as historical context, but current source code is authoritative if the two disagree.
- Keep server-side API shapes simple and serialization-friendly for `System.Text.Json`.