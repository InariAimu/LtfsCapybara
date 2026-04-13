---
name: ltfs-capybara-repo-memory
description: 'Use when working on LtfsCapybara LTFS, TapeDrive, LtfsServer tasks APIs, barcode flows, or ltfs-capybara-gui frontend integration. Provides repository memory for GUI build constraints, tape incident handling, variable-length SCSI field parsing, and task-model background.'
argument-hint: 'Mention the subsystem you are touching, such as gui, tapedrive, ltfs, server, tasks, or barcode.'
user-invocable: true
---

# Ltfs Capybara Repository Memory

Use this skill when a task touches repository-specific behavior that is easy to miss from local file context alone.

## When To Use

- Editing `TapeDrive` SCSI commands, struct parsing, sense handling, or incident escalation.
- Editing `Ltfs` task, commit, format, or barcode-related flows.
- Editing `LtfsServer` task APIs, DTOs, or tape-group execution paths.
- Editing `ltfs-capybara-gui` views, API adapters, or format-task payloads.

## Procedure

1. Match the task to the closest reference below.
2. Apply the repository-specific constraints from that reference before making edits.
3. If a reference conflicts with current source code, trust current code and treat the reference as historical background.
4. When the task spans multiple subsystems, load more than one reference.

## References

- GUI constraints and payload alignment: [gui.md](./references/gui.md)
- Tape incident handling and SCSI struct parsing: [tape-drive.md](./references/tape-drive.md)
- Task and API background: [tasks.md](./references/tasks.md)