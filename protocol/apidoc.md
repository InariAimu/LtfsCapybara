# LtfsServer API Documentation (for AI Coding)

This document summarizes all HTTP endpoints currently exposed by LtfsServer, based on code in the API layer and route registration.

## Source Scope

- API modules under LtfsServer/API
- Route registration in LtfsServer/Program.cs
- Request/response models in LtfsServer/Services

## Base URL

- Default runtime URL is configured in server startup.
- Local dev commonly uses: http://localhost:5003

## General Conventions

- Content-Type: application/json
- Success responses usually return HTTP 200 with JSON
- Validation/domain errors generally return HTTP 400
- Not-found resources generally return HTTP 404
- Some internal failures return HTTP 500/problem payload
- Timestamps in this API are typically .NET ticks (long) or ISO strings, depending on endpoint

## Quick Endpoint Index

- GET /
- GET /api/health
- GET /api/info
- GET /api/example/files
- GET /api/tapedrives
- GET /api/tapedrives/{id}/machine
- POST /api/tapedrives/{id}/machine/{action}
- GET /api/localtapes
- GET /api/local/{tapeName}
- GET /api/local/{tapeName}/{**path}
- GET /api/localcm/{tapeName}
- GET /api/fsroots
- GET /api/fschildren?path=...
- GET /api/fsfiles?path=...
- GET /api/settings/server
- PUT /api/settings/server
- GET /api/tasks/groups
- GET /api/tasks/groups/{tapeBarcode}
- POST /api/tasks/groups/{tapeBarcode}/rename
- POST /api/tasks/groups/{tapeBarcode}/tasks
- POST /api/tasks/groups/{tapeBarcode}/tasks/server-folder
- POST /api/tasks/groups/{tapeBarcode}/tasks/format
- DELETE /api/tasks/groups/{tapeBarcode}/tasks/{taskId}

---

## 1) Server/Health

### GET /
Returns basic server running message.

Response (200):
```json
{
  "message": "LtfsServer running",
  "env": "Development",
  "url": "..."
}
```

### GET /api/health
Response (200):
```json
{
  "status": "OK",
  "timestamp": "2026-03-31T12:34:56.789Z"
}
```

### GET /api/info
Response (200):
```json
{
  "name": "LtfsServer",
  "version": "1.0.0.0"
}
```

### GET /api/example/files
Response (200):
```json
[
  { "Name": "example1.txt", "Size": 1234 },
  { "Name": "example2.bin", "Size": 987654 }
]
```

---

## 2) Tape Drive APIs

### GET /api/tapedrives
Scans and synchronizes tape drives, then returns the current list.

Response (200):
```json
[
  {
    "id": "tape0",
    "devicePath": "\\\\.\\Tape0",
    "displayName": "Fake Tape0",
    "isFake": true
  }
]
```

### GET /api/tapedrives/{id}/machine
Returns current tape machine snapshot for the given drive id.

Path params:
- id: drive identifier (example: tape0)

Success response (200):
```json
{
  "tapeDriveId": "tape0",
  "devicePath": "\\\\.\\Tape0",
  "state": "Empty",
  "allowedActions": ["ThreadTape", "LoadTape"],
  "lastError": null,
  "isFake": true,
  "cartridgeMemory": null
}
```

Errors:
- 404 when drive id not found
- 400 on other command/snapshot errors

### POST /api/tapedrives/{id}/machine/{action}
Executes a tape machine action and returns updated snapshot.

Path params:
- id: drive identifier
- action: one of
  - thread
  - load
  - unthread
  - eject
  - read-info

Success response (200): same shape as machine snapshot.

Errors:
- 400 for unsupported action
- 404 for unknown drive
- 400 for invalid action in current state

AI coding notes:
- Always call GET /api/tapedrives first, then machine snapshot/action APIs.
- Use allowedActions from snapshot to gate UI actions.

---

## 3) Local Tape Metadata APIs

### GET /api/localtapes
Returns tape summaries inferred from latest cartridge memory files.

Response (200):
```json
[
  {
    "tapeName": "ABC123L6",
    "cmFileName": "ABC123L6_20260331_120101.1234567.cmbin",
    "generation": 6,
    "particleType": "MP",
    "vendor": "FUJIFILM",
    "totalSizeBytes": 2500000000000,
    "freeSizeBytes": 1700000000000,
    "ticks": 638790000000000000
  }
]
```

### GET /api/localcm/{tapeName}
Loads and parses the latest .cm/.cmbin file for a tape.

Path params:
- tapeName: barcode / local tape directory name

Success response (200): raw CartridgeMemory object (large nested payload).

Errors:
- 404 if no cartridge memory file found
- 404 if resolved file does not exist
- 500 problem response if parsing fails

AI coding notes:
- Treat the response as schema-flexible object; only read fields needed by UI.

---

## 4) Local LTFS Index APIs

### GET /api/local/{tapeName}
### GET /api/local/{tapeName}/{**path}
Returns a directory DTO from the latest local LTFS XML index, overlaid with task group actions for the same tape barcode.

Path params:
- tapeName: barcode / local tape directory name
- path (optional catch-all): LTFS path (slashes normalized)

Response (200):
```json
{
  "name": "ABC123L6",
  "items": [
    {
      "type": "dir",
      "name": "docs",
      "taskType": "add",
      "count": 12
    },
    {
      "type": "file",
      "name": "readme.txt",
      "size": 12345,
      "index": "...",
      "crc64": "...",
      "createTime": "...",
      "modifyTime": "...",
      "updateTime": "...",
      "backupTime": "...",
      "taskType": "delete"
    }
  ]
}
```

taskType values observed:
- dir items from overlay: add, delete
- file items from overlay: add, rename, replace, delete

Errors:
- 404 if no index and no tasks for tape
- 404 if requested path is not resolvable by index or overlay
- 500 if index XML load fails

Path normalization behavior:
- Backslashes are converted to /
- Empty or / means root
- Duplicate slashes collapse

AI coding notes:
- Use this endpoint as the primary tree source for tape content UI because it merges persisted index + pending tasks.
- Do not assume every file field exists for dir items.

---

## 5) Local File System Browse APIs

### GET /api/fsroots
Returns local roots and discovered network roots.

Response (200):
```json
{
  "items": [
    {
      "id": "C:\\\\",
      "name": "C:",
      "path": "C:\\\\",
      "kind": "drive",
      "available": true,
      "hasChildren": true,
      "error": null
    }
  ],
  "loadedAtUtc": "2026-03-31T12:34:56.789Z"
}
```

kind values:
- drive
- network

### GET /api/fschildren?path=...
Returns child directories under path.

Query params:
- path (required): absolute local path or UNC path

Response (200):
```json
{
  "parentPath": "C:\\\\data",
  "items": [
    {
      "id": "C:\\\\data\\\\folderA",
      "name": "folderA",
      "path": "C:\\\\data\\\\folderA",
      "kind": "dir",
      "available": true,
      "hasChildren": false,
      "error": null
    }
  ],
  "warning": null,
  "loadedAtUtc": "2026-03-31T12:34:56.789Z"
}
```

Errors:
- 400 if path missing or invalid
- 404 if directory not found

### GET /api/fsfiles?path=...
Returns files under path.

Query params:
- path (required)

Response (200):
```json
{
  "parentPath": "C:\\\\data",
  "files": [
    {
      "name": "a.txt",
      "path": "C:\\\\data\\\\a.txt",
      "size": 1024
    }
  ],
  "warning": null,
  "loadedAtUtc": "2026-03-31T12:34:56.789Z"
}
```

Errors:
- 400 if path missing or invalid
- 404 if directory not found

AI coding notes:
- URL-encode query path values.
- Network discovery may be partial/slow; handle empty results and warning text gracefully.

---

## 6) Server Settings APIs

### GET /api/settings/server
Response (200):
```json
{
  "indexOnDataPartitionId": 1,
  "indexOnIndexPartitionId": 1,
  "dataPath": "E:\\\\LtfsData"
}
```

### PUT /api/settings/server
Request body:
```json
{
  "indexOnDataPartitionId": 1,
  "indexOnIndexPartitionId": 2,
  "dataPath": "E:\\\\LtfsData"
}
```

Validation:
- indexOnDataPartitionId and indexOnIndexPartitionId must be between 1 and 2

Success response (200): same shape as GET response.

Errors:
- 400 for invalid option ids
- 500/problem if appsettings read/write fails

---

## 7) Task Group APIs

Task groups are keyed by tape barcode and persisted on server.

### Data Models

TapeFsTaskGroup:
```json
{
  "tapeBarcode": "ABC123L6",
  "name": "My Tape",
  "tasks": [
    {
      "id": "...",
      "type": "add",
      "tapeBarcode": "ABC123L6",
      "pathTask": {
        "isDirectory": false,
        "operation": "add",
        "path": "/dir/file.txt",
        "newPath": null,
        "localPath": "C:\\\\source\\\\file.txt"
      },
      "readTask": null,
      "formatTask": null,
      "createdAtTicks": 638790000000000000
    }
  ],
  "updatedAtTicks": 638790000000000000
}
```

Task type values:
- add
- rename
- update
- delete
- read
- format

Legacy aliases accepted for input type/operation normalization:
- write -> add
- replace -> update
- folder -> add

### GET /api/tasks/groups
Returns all task groups.

### GET /api/tasks/groups/{tapeBarcode}
Gets or creates a task group by tape barcode.

Errors:
- 400 if tape barcode is empty/invalid

### POST /api/tasks/groups/{tapeBarcode}/rename
Request body:
```json
{
  "name": "Project Tape A"
}
```

Success response: updated group.

Errors:
- 400 on invalid tape barcode or empty name

### POST /api/tasks/groups/{tapeBarcode}/tasks
Creates one task in group.

Request body:
```json
{
  "type": "add",
  "tapeBarcode": "optional-client-field",
  "pathTask": {
    "isDirectory": false,
    "operation": "add",
    "path": "/dst/file.txt",
    "newPath": null,
    "localPath": "C:\\\\src\\\\file.txt"
  },
  "readTask": null,
  "formatTask": null
}
```

Rules by type:
- add/update/delete/rename require pathTask
- read uses readTask (pathTask not required)
- format uses formatTask, and must be unique and first in group

PathTask rules:
- operation must match type
- IsDirectory=true: path/newPath are folder paths
- IsDirectory=false: path/newPath are file paths
- File add/update requires existing localPath file
- delete can omit localPath

Success response: updated group.

Errors:
- 400 validation or state errors
- 404 if referenced local file/dir not found

### POST /api/tasks/groups/{tapeBarcode}/tasks/server-folder
Bulk-adds tasks by scanning a local directory recursively and mapping to target tape path.

Request body:
```json
{
  "localPath": "C:\\\\sourceFolder",
  "targetPath": "/TapeFolder"
}
```

Behavior:
- Adds a directory add task for each folder.
- Adds a file add task for each file.

Success response: updated group.

Errors:
- 400 invalid args/state
- 404 localPath not found

### POST /api/tasks/groups/{tapeBarcode}/tasks/format
Adds format task to group.

Request body:
```json
{
  "formatTask": {
    "formatParam": {
      "barcode": "ABC123L6"
    }
  }
}
```

Notes:
- Body can be null/empty; server creates default format task.
- If formatTask.formatParam.barcode is empty, server fills group barcode.
- Only one format task allowed per group.

Success response: updated group.

Errors:
- 400 invalid barcode or duplicate format task

### DELETE /api/tasks/groups/{tapeBarcode}/tasks/{taskId}
Deletes task by id.

Success response: updated group.

Errors:
- 400 invalid barcode/task id
- 404 group or task not found

---

## 8) Error Payload Patterns

Common shapes:

```json
{ "error": "..." }
```

```json
{ "message": "..." }
```

ProblemDetails-style (some 500 cases):
```json
{
  "type": "...",
  "title": "...",
  "status": 500,
  "detail": "..."
}
```

AI coding recommendation:
- Treat both error and message as equivalent server error text channels.

---

## 9) AI Integration Checklist

When generating client code for this API:

1. Always implement typed wrappers per endpoint group (tapedrives, local, fs, settings, tasks).
2. Normalize tape paths to slash-based absolute paths (for task payloads).
3. URL-encode all path query values for fschildren/fsfiles.
4. Handle mixed error payloads: error, message, and ProblemDetails.
5. For task workflows, fetch group first, then mutate, then refresh from response.
6. For machine actions, read allowedActions before posting action.
7. For local index browsing, use overlayed tree endpoints (/api/local/...) as source of truth for pending operations.

## 10) Suggested Future Enhancements

- Add OpenAPI/Swagger generation for strict schema contracts.
- Standardize all errors to RFC7807 ProblemDetails.
- Add explicit DTO classes for LocalIndex responses and document with examples.
- Add pagination for very large folder/file listings.
