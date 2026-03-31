using System.Text.Json;

using Ltfs;

using LtfsServer.BootStrap;

namespace LtfsServer.Features.Tasks;

public sealed class TaskGroupService : ITaskGroupService
{
    private readonly object _syncRoot = new();
    private readonly string _storePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly Dictionary<string, TapeFsTaskGroup> _groups = new(StringComparer.OrdinalIgnoreCase);

    public TaskGroupService(AppData appData)
    {
        var dir = Path.Combine(appData.Path, "tasks");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "task-groups.json");

        LoadFromDisk();
    }

    public IReadOnlyList<TapeFsTaskGroup> ListGroups()
    {
        lock (_syncRoot)
        {
            return _groups.Values
                .OrderBy(g => g.TapeBarcode, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToArray();
        }
    }

    public TapeFsTaskGroup GetOrCreateGroup(string tapeBarcode)
    {
        lock (_syncRoot)
        {
            var key = NormalizeBarcode(tapeBarcode);
            var group = GetOrCreateGroupCore(key);
            return Clone(group);
        }
    }

    public TapeFsTaskGroup RenameGroup(string tapeBarcode, string name)
    {
        lock (_syncRoot)
        {
            var key = NormalizeBarcode(tapeBarcode);
            var normalizedName = (name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
                throw new ArgumentException("Task group name is required.");

            var group = GetOrCreateGroupCore(key);
            group.Name = normalizedName;
            group.UpdatedAtTicks = DateTime.UtcNow.Ticks;
            ValidateGroup(group, validateLocalPaths: false);
            SaveToDisk();
            return Clone(group);
        }
    }

    public TapeFsTaskGroup AddTask(string tapeBarcode, TapeFsTaskCreateRequest request)
    {
        lock (_syncRoot)
        {
            var key = NormalizeBarcode(tapeBarcode);
            var type = NormalizeTaskType(request.Type);
            var group = GetOrCreateGroupCore(key);
            var task = BuildTask(group, type, request);

            if (type == TapeFsTaskType.Format)
            {
                EnsureNoFormatTask(group);
                group.Tasks.Insert(0, task);
            }
            else
            {
                group.Tasks.Add(task);
            }

            group.UpdatedAtTicks = DateTime.UtcNow.Ticks;
            ValidateGroup(group, validateLocalPaths: false);
            SaveToDisk();
            return Clone(group);
        }
    }

    public TapeFsTaskGroup AddServerFolderTask(string tapeBarcode, AddTapeFsServerFolderTaskRequest request)
    {
        lock (_syncRoot)
        {
            var key = NormalizeBarcode(tapeBarcode);
            var localPath = NormalizeLocalDirectoryPath(request.LocalPath);
            var targetPath = NormalizeFolderPath(request.TargetPath);
            var group = GetOrCreateGroupCore(key);

            foreach (var task in BuildServerFolderTasks(key, localPath, targetPath))
            {
                group.Tasks.Add(task);
            }

            group.UpdatedAtTicks = DateTime.UtcNow.Ticks;
            ValidateGroup(group, validateLocalPaths: false);
            SaveToDisk();
            return Clone(group);
        }
    }

    public TapeFsTaskGroup AddFormatTask(string tapeBarcode, FormatTask? formatTask = null)
    {
        lock (_syncRoot)
        {
            var key = NormalizeBarcode(tapeBarcode);
            var group = GetOrCreateGroupCore(key);
            EnsureNoFormatTask(group);

            var safeFormatTask = formatTask ?? new FormatTask
            {
                FormatParam = new FormatParam(),
            };
            if (string.IsNullOrWhiteSpace(safeFormatTask.FormatParam.Barcode))
                safeFormatTask.FormatParam.Barcode = key;

            var task = new TapeFsTask
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = TapeFsTaskType.Format,
                TapeBarcode = key,
                FormatTask = safeFormatTask,
                CreatedAtTicks = DateTime.UtcNow.Ticks,
            };

            group.Tasks.Insert(0, task);
            group.UpdatedAtTicks = DateTime.UtcNow.Ticks;
            ValidateGroup(group, validateLocalPaths: false);
            SaveToDisk();
            return Clone(group);
        }
    }

    public TapeFsTaskGroup DeleteTask(string tapeBarcode, string taskId)
    {
        lock (_syncRoot)
        {
            var key = NormalizeBarcode(tapeBarcode);
            var normalizedTaskId = (taskId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedTaskId))
                throw new ArgumentException("Task id is required.");

            if (!_groups.TryGetValue(key, out var group))
                throw new KeyNotFoundException($"Task group for tape '{key}' was not found.");

            var removed = group.Tasks.RemoveAll(t => string.Equals(t.Id, normalizedTaskId, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
                throw new KeyNotFoundException($"Task '{normalizedTaskId}' was not found.");

            group.UpdatedAtTicks = DateTime.UtcNow.Ticks;
            ValidateGroup(group, validateLocalPaths: false);
            SaveToDisk();
            return Clone(group);
        }
    }

    private TapeFsTask BuildTask(TapeFsTaskGroup group, string type, TapeFsTaskCreateRequest request)
    {
        var task = new TapeFsTask
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = type,
            TapeBarcode = group.TapeBarcode,
            CreatedAtTicks = DateTime.UtcNow.Ticks,
        };

        if (type == TapeFsTaskType.Read)
        {
            task.ReadTask = request.ReadTask ?? new ReadTask();
            return task;
        }

        if (type == TapeFsTaskType.Format)
        {
            var formatTask = request.FormatTask ?? new FormatTask
            {
                FormatParam = new FormatParam(),
            };
            if (string.IsNullOrWhiteSpace(formatTask.FormatParam.Barcode))
                formatTask.FormatParam.Barcode = group.TapeBarcode;

            task.FormatTask = formatTask;
            return task;
        }

        var pathTask = BuildPathTask(type, request);
        task.PathTask = NormalizePathTask(pathTask, type, validateLocalPath: true);
        return task;
    }

    private static TapeFsPathTask BuildPathTask(string type, TapeFsTaskCreateRequest request)
    {
        if (request.PathTask is null)
            throw new ArgumentException("Path task payload is required.");

        return request.PathTask;
    }

    private static TapeFsPathTask NormalizePathTask(TapeFsPathTask pathTask, string expectedType, bool validateLocalPath)
    {
        pathTask.Operation = NormalizeOperation(pathTask.Operation, expectedType);

        if (pathTask.IsDirectory)
        {
            pathTask.Path = NormalizeFolderPath(pathTask.Path);
            if (pathTask.Operation == TapeFsTaskType.Rename)
                pathTask.NewPath = NormalizeFolderPath(pathTask.NewPath ?? string.Empty);
            else
            {
                pathTask.NewPath = null;
            }

            pathTask.LocalPath = string.IsNullOrWhiteSpace(pathTask.LocalPath)
                ? string.Empty
                : NormalizeLocalPathLoose(pathTask.LocalPath);
            if (pathTask.Operation == TapeFsTaskType.Update)
                pathTask.Operation = TapeFsTaskType.Rename;
            return pathTask;
        }

        pathTask.Path = NormalizeFilePath(pathTask.Path);
        if (pathTask.Operation == TapeFsTaskType.Rename)
        {
            pathTask.NewPath = NormalizeFilePath(pathTask.NewPath ?? string.Empty);
            pathTask.LocalPath = string.IsNullOrWhiteSpace(pathTask.LocalPath)
                ? string.Empty
                : NormalizeLocalPathLoose(pathTask.LocalPath);
            return pathTask;
        }

        pathTask.NewPath = null;
        if (pathTask.Operation is TapeFsTaskType.Add or TapeFsTaskType.Update)
        {
            pathTask.LocalPath = validateLocalPath
                ? NormalizeLocalFilePath(pathTask.LocalPath)
                : NormalizeLocalPathLoose(pathTask.LocalPath);
            return pathTask;
        }

        pathTask.LocalPath = NormalizeLocalPathForDelete(pathTask.LocalPath);
        return pathTask;
    }

    private static IEnumerable<TapeFsTask> BuildServerFolderTasks(string tapeBarcode, string localRootPath, string targetRootPath)
    {
        var queue = new Queue<(string LocalDir, string TapeDir)>();
        queue.Enqueue((localRootPath, targetRootPath));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            yield return new TapeFsTask
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = TapeFsTaskType.Add,
                TapeBarcode = tapeBarcode,
                PathTask = new TapeFsPathTask
                {
                    IsDirectory = true,
                    Operation = TapeFsTaskType.Add,
                    Path = current.TapeDir,
                },
                CreatedAtTicks = DateTime.UtcNow.Ticks,
            };

            foreach (var filePath in Directory.EnumerateFiles(current.LocalDir).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileName(filePath);
                var targetPath = CombineTapePath(current.TapeDir, fileName);

                yield return new TapeFsTask
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Type = TapeFsTaskType.Add,
                    TapeBarcode = tapeBarcode,
                    PathTask = new TapeFsPathTask
                    {
                        IsDirectory = false,
                        Operation = TapeFsTaskType.Add,
                        LocalPath = filePath,
                        Path = targetPath,
                    },
                    CreatedAtTicks = DateTime.UtcNow.Ticks,
                };
            }

            foreach (var directoryPath in Directory.EnumerateDirectories(current.LocalDir).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var directoryName = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                queue.Enqueue((directoryPath, CombineTapePath(current.TapeDir, directoryName)));
            }
        }
    }

    private TapeFsTaskGroup GetOrCreateGroupCore(string tapeBarcode)
    {
        if (_groups.TryGetValue(tapeBarcode, out var existing))
            return existing;

        var now = DateTime.UtcNow.Ticks;
        var group = new TapeFsTaskGroup
        {
            TapeBarcode = tapeBarcode,
            Name = tapeBarcode,
            Tasks = [],
            UpdatedAtTicks = now,
        };
        _groups[tapeBarcode] = group;
        SaveToDisk();
        return group;
    }

    private static void EnsureNoFormatTask(TapeFsTaskGroup group)
    {
        if (group.Tasks.Any(t => string.Equals(t.Type, TapeFsTaskType.Format, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Format task already exists for this tape.");
    }

    private static bool IsPathTaskType(string type)
    {
        return type is TapeFsTaskType.Add or TapeFsTaskType.Rename or TapeFsTaskType.Update or TapeFsTaskType.Delete;
    }

    private static string NormalizeBarcode(string tapeBarcode)
    {
        var normalized = (tapeBarcode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Tape barcode is required.");

        return normalized;
    }

    private static string NormalizeTaskType(string type)
    {
        var normalized = (type ?? string.Empty).Trim().ToLowerInvariant();
        normalized = normalized switch
        {
            TapeFsLegacyTaskType.Write => TapeFsTaskType.Add,
            TapeFsLegacyTaskType.Replace => TapeFsTaskType.Update,
            TapeFsLegacyTaskType.Folder => TapeFsTaskType.Add,
            _ => normalized,
        };

        if (!TapeFsTaskType.IsValid(normalized))
            throw new ArgumentException($"Unsupported task type '{type}'.");

        return normalized;
    }

    private static string NormalizeOperation(string operation, string expectedType)
    {
        var normalized = NormalizeTaskType(operation);
        if (!string.Equals(normalized, expectedType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Path task operation '{operation}' does not match task type '{expectedType}'.");

        if (!IsPathTaskType(normalized))
            throw new ArgumentException($"Task type '{expectedType}' does not support path task payload.");

        return normalized;
    }

    private static string NormalizeFolderPath(string path)
    {
        var normalized = (path ?? string.Empty).Trim().Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Folder path is required.");

        normalized = normalized == "/" ? "/" : normalized.Trim('/');
        normalized = normalized == "/" ? "/" : "/" + normalized;

        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        return normalized;
    }

    private static string NormalizeFilePath(string path)
    {
        var normalized = NormalizeFolderPath(path);
        if (normalized == "/")
            throw new ArgumentException("File target path cannot be root.");

        return normalized;
    }

    private static string NormalizeLocalDirectoryPath(string path)
    {
        var trimmed = (path ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Local directory path is required.");

        var normalized = Path.GetFullPath(trimmed);
        if (!Directory.Exists(normalized))
            throw new DirectoryNotFoundException($"Directory '{trimmed}' was not found.");

        return normalized;
    }

    private static string NormalizeLocalFilePath(string path)
    {
        var trimmed = (path ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Local file path is required.");

        var normalized = Path.GetFullPath(trimmed);
        if (!File.Exists(normalized))
            throw new FileNotFoundException($"File '{trimmed}' was not found.", normalized);

        return normalized;
    }

    private static string NormalizeLocalPathForDelete(string path)
    {
        var trimmed = (path ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? string.Empty : Path.GetFullPath(trimmed);
    }

    private static string NormalizeLocalPathLoose(string path)
    {
        var trimmed = (path ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Local file path is required.");

        return Path.GetFullPath(trimmed);
    }

    private static string CombineTapePath(string parentPath, string childName)
    {
        var normalizedParent = NormalizeFolderPath(parentPath);
        var trimmedChild = (childName ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(trimmedChild))
            throw new ArgumentException("Path segment is required.");

        return normalizedParent == "/" ? "/" + trimmedChild : normalizedParent + "/" + trimmedChild;
    }

    private static TapeFsPathTask? MigrateLegacyPathTask(TapeFsTask task)
    {
        if (task.FolderTask is not null)
        {
            return new TapeFsPathTask
            {
                IsDirectory = true,
                Operation = task.FolderTask.TaskType,
                Path = task.FolderTask.Path,
            };
        }

        if (task.WriteTask is null)
            return null;

        var operation = task.WriteTask.TaskType switch
        {
            FileTaskType.Write => TapeFsTaskType.Add,
            FileTaskType.Replace => TapeFsTaskType.Update,
            FileTaskType.Delete => TapeFsTaskType.Delete,
            _ => task.Type,
        };

        return new TapeFsPathTask
        {
            IsDirectory = false,
            Operation = operation,
            LocalPath = task.WriteTask.LocalPath,
            Path = task.WriteTask.TargetPath,
        };
    }

    private static void ValidateGroup(TapeFsTaskGroup group, bool validateLocalPaths)
    {
        var formatIndexes = group.Tasks
            .Select((task, index) => new { task, index })
            .Where(entry => string.Equals(entry.task.Type, TapeFsTaskType.Format, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.index)
            .ToArray();

        if (formatIndexes.Length > 1)
            throw new InvalidOperationException("Format task must be unique in a tape task group.");

        if (formatIndexes.Length == 1 && formatIndexes[0] != 0)
            throw new InvalidOperationException("Format task must be the first task in the tape task group.");

        foreach (var task in group.Tasks)
        {
            if (!string.Equals(task.TapeBarcode, group.TapeBarcode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Task barcode must match task group barcode.");

            task.Type = NormalizeTaskType(task.Type);

            if (IsPathTaskType(task.Type))
            {
                task.PathTask ??= MigrateLegacyPathTask(task)
                    ?? throw new InvalidOperationException("Path task payload is required.");
                task.PathTask = NormalizePathTask(task.PathTask, task.Type, validateLocalPaths);
            }

            if (task.Type == TapeFsTaskType.Read)
                task.ReadTask ??= new ReadTask();

            if (task.Type == TapeFsTaskType.Format)
            {
                task.FormatTask ??= new FormatTask
                {
                    FormatParam = new FormatParam(),
                };
                if (string.IsNullOrWhiteSpace(task.FormatTask.FormatParam.Barcode))
                    task.FormatTask.FormatParam.Barcode = group.TapeBarcode;
            }

            task.WriteTask = null;
            task.FolderTask = null;
        }
    }

    private void LoadFromDisk()
    {
        lock (_syncRoot)
        {
            if (!File.Exists(_storePath))
                return;

            var json = File.ReadAllText(_storePath);
            if (string.IsNullOrWhiteSpace(json))
                return;

            var groups = JsonSerializer.Deserialize<List<TapeFsTaskGroup>>(json);
            if (groups is null)
                return;

            _groups.Clear();
            foreach (var group in groups)
            {
                if (string.IsNullOrWhiteSpace(group.TapeBarcode))
                    continue;

                group.TapeBarcode = NormalizeBarcode(group.TapeBarcode);
                group.Name = string.IsNullOrWhiteSpace(group.Name) ? group.TapeBarcode : group.Name.Trim();
                group.Tasks ??= [];

                foreach (var task in group.Tasks)
                {
                    task.TapeBarcode = group.TapeBarcode;
                    task.Id = string.IsNullOrWhiteSpace(task.Id) ? Guid.NewGuid().ToString("N") : task.Id.Trim();
                    task.CreatedAtTicks = task.CreatedAtTicks == 0 ? DateTime.UtcNow.Ticks : task.CreatedAtTicks;
                }

                group.UpdatedAtTicks = group.UpdatedAtTicks == 0 ? DateTime.UtcNow.Ticks : group.UpdatedAtTicks;
                ValidateGroup(group, validateLocalPaths: false);
                _groups[group.TapeBarcode] = group;
            }
        }
    }

    private void SaveToDisk()
    {
        var tempPath = _storePath + ".tmp";
        var payload = _groups.Values
            .OrderBy(g => g.TapeBarcode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var json = JsonSerializer.Serialize(payload, _jsonOptions);

        File.WriteAllText(tempPath, json);
        File.Copy(tempPath, _storePath, true);
        File.Delete(tempPath);
    }

    private static TapeFsTaskGroup Clone(TapeFsTaskGroup group)
    {
        var json = JsonSerializer.Serialize(group);
        var cloned = JsonSerializer.Deserialize<TapeFsTaskGroup>(json);
        return cloned ?? new TapeFsTaskGroup
        {
            TapeBarcode = group.TapeBarcode,
            Name = group.Name,
            Tasks = [],
            UpdatedAtTicks = group.UpdatedAtTicks,
        };
    }
}
