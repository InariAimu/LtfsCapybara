using System.Text.Json;
using Ltfs;
using Ltfs.Index;

namespace LtfsServer.Services;

public interface ITaskGroupService
{
    IReadOnlyList<LtfsTaskGroup> ListGroups();
    LtfsTaskGroup GetOrCreateGroup(string tapeBarcode);
    LtfsTaskGroup RenameGroup(string tapeBarcode, string name);
    LtfsTaskGroup AddTask(string tapeBarcode, LtfsTaskCreateRequest request);
    LtfsTaskGroup AddServerFolderTask(string tapeBarcode, AddServerFolderTaskRequest request);
    LtfsTaskGroup AddFormatTask(string tapeBarcode, FormatTask? formatTask = null);
    LtfsTaskGroup DeleteTask(string tapeBarcode, string taskId);
}

public static class LtfsTaskType
{
    public const string Write = "write";
    public const string Replace = "replace";
    public const string Delete = "delete";
    public const string Read = "read";
    public const string Format = "format";
    public const string Folder = "folder";

    public static bool IsValid(string type)
    {
        return type is Write or Replace or Delete or Read or Format or Folder;
    }
}

public static class FolderTaskType
{
    public const string Add = "add";
    public const string Delete = "delete";

    public static bool IsValid(string type)
    {
        return type is Add or Delete;
    }
}

public sealed class FolderTask
{
    public string TaskType { get; set; } = FolderTaskType.Add;
    public string Path { get; set; } = "/";
}

public sealed class LtfsTaskGroup
{
    public string TapeBarcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<LtfsTaskItem> Tasks { get; set; } = [];
    public long UpdatedAtTicks { get; set; }
}

public sealed class LtfsTaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = string.Empty;
    public string TapeBarcode { get; set; } = string.Empty;
    public WriteTask? WriteTask { get; set; }
    public ReadTask? ReadTask { get; set; }
    public FormatTask? FormatTask { get; set; }
    public FolderTask? FolderTask { get; set; }
    public long CreatedAtTicks { get; set; } = DateTime.UtcNow.Ticks;
}

public sealed class LtfsWriteTaskRequest
{
    public string? TaskType { get; set; }
    public string LocalPath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
}

public sealed class LtfsTaskCreateRequest
{
    public string Type { get; set; } = string.Empty;
    public string TapeBarcode { get; set; } = string.Empty;
    public LtfsWriteTaskRequest? WriteTask { get; set; }
    public ReadTask? ReadTask { get; set; }
    public FormatTask? FormatTask { get; set; }
    public FolderTask? FolderTask { get; set; }
}

public sealed class AddServerFolderTaskRequest
{
    public string LocalPath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
}

public sealed class RenameTaskGroupRequest
{
    public string Name { get; set; } = string.Empty;
}

public sealed class AddFormatTaskRequest
{
    public FormatTask? FormatTask { get; set; }
}

public sealed class TaskGroupService : ITaskGroupService
{
    private readonly object _syncRoot = new();
    private readonly string _storePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly Dictionary<string, LtfsTaskGroup> _groups = new(StringComparer.OrdinalIgnoreCase);

    public TaskGroupService(AppData appData)
    {
        var dir = Path.Combine(appData.Path, "tasks");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "task-groups.json");

        LoadFromDisk();
    }

    public IReadOnlyList<LtfsTaskGroup> ListGroups()
    {
        lock (_syncRoot)
        {
            return _groups.Values
                .OrderBy(g => g.TapeBarcode, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToArray();
        }
    }

    public LtfsTaskGroup GetOrCreateGroup(string tapeBarcode)
    {
        lock (_syncRoot)
        {
            var key = NormalizeBarcode(tapeBarcode);
            var group = GetOrCreateGroupCore(key);
            return Clone(group);
        }
    }

    public LtfsTaskGroup RenameGroup(string tapeBarcode, string name)
    {
        lock (_syncRoot)
        {
            var key = NormalizeBarcode(tapeBarcode);
            var normalizedName = (name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new ArgumentException("Task group name is required.");
            }

            var group = GetOrCreateGroupCore(key);
            group.Name = normalizedName;
            group.UpdatedAtTicks = DateTime.UtcNow.Ticks;
            ValidateGroup(group);
            SaveToDisk();
            return Clone(group);
        }
    }

    public LtfsTaskGroup AddTask(string tapeBarcode, LtfsTaskCreateRequest request)
    {
        lock (_syncRoot)
        {
            var key = NormalizeBarcode(tapeBarcode);
            var type = NormalizeTaskType(request.Type);
            var group = GetOrCreateGroupCore(key);
            var task = BuildTask(group, type, request);

            if (type == LtfsTaskType.Format)
            {
                EnsureNoFormatTask(group);
                group.Tasks.Insert(0, task);
            }
            else
            {
                group.Tasks.Add(task);
            }

            group.UpdatedAtTicks = DateTime.UtcNow.Ticks;
            ValidateGroup(group);
            SaveToDisk();
            return Clone(group);
        }
    }

    public LtfsTaskGroup AddServerFolderTask(string tapeBarcode, AddServerFolderTaskRequest request)
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
            ValidateGroup(group);
            SaveToDisk();
            return Clone(group);
        }
    }

    public LtfsTaskGroup AddFormatTask(string tapeBarcode, FormatTask? formatTask = null)
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
            {
                safeFormatTask.FormatParam.Barcode = key;
            }

            var task = new LtfsTaskItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = LtfsTaskType.Format,
                TapeBarcode = key,
                FormatTask = safeFormatTask,
                CreatedAtTicks = DateTime.UtcNow.Ticks,
            };

            group.Tasks.Insert(0, task);
            group.UpdatedAtTicks = DateTime.UtcNow.Ticks;
            ValidateGroup(group);
            SaveToDisk();
            return Clone(group);
        }
    }

    public LtfsTaskGroup DeleteTask(string tapeBarcode, string taskId)
    {
        lock (_syncRoot)
        {
            var key = NormalizeBarcode(tapeBarcode);
            var normalizedTaskId = (taskId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedTaskId))
            {
                throw new ArgumentException("Task id is required.");
            }

            if (!_groups.TryGetValue(key, out var group))
            {
                throw new KeyNotFoundException($"Task group for tape '{key}' was not found.");
            }

            var removed = group.Tasks.RemoveAll(t => string.Equals(t.Id, normalizedTaskId, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                throw new KeyNotFoundException($"Task '{normalizedTaskId}' was not found.");
            }

            group.UpdatedAtTicks = DateTime.UtcNow.Ticks;
            ValidateGroup(group);
            SaveToDisk();
            return Clone(group);
        }
    }

    private LtfsTaskItem BuildTask(LtfsTaskGroup group, string type, LtfsTaskCreateRequest request)
    {
        var task = new LtfsTaskItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = type,
            TapeBarcode = group.TapeBarcode,
            CreatedAtTicks = DateTime.UtcNow.Ticks,
        };

        if (type is LtfsTaskType.Write or LtfsTaskType.Replace or LtfsTaskType.Delete)
        {
            var writeTask = CreateWriteTask(type, request.WriteTask);
            task.WriteTask = writeTask;
            return task;
        }

        if (type == LtfsTaskType.Read)
        {
            task.ReadTask = request.ReadTask ?? new ReadTask();
            return task;
        }

        if (type == LtfsTaskType.Format)
        {
            var formatTask = request.FormatTask ?? new FormatTask
            {
                FormatParam = new FormatParam(),
            };
            if (string.IsNullOrWhiteSpace(formatTask.FormatParam.Barcode))
            {
                formatTask.FormatParam.Barcode = group.TapeBarcode;
            }

            task.FormatTask = formatTask;
            return task;
        }

        if (type == LtfsTaskType.Folder)
        {
            var folderTask = request.FolderTask ?? new FolderTask();
            folderTask.TaskType = NormalizeFolderTaskType(folderTask.TaskType);
            folderTask.Path = NormalizeFolderPath(folderTask.Path);

            task.FolderTask = folderTask;
            return task;
        }

        throw new ArgumentException($"Unsupported task type '{type}'.");
    }

    private static WriteTask CreateWriteTask(string type, LtfsWriteTaskRequest? request)
    {
        if (request is null)
        {
            throw new ArgumentException("Write task payload is required.");
        }

        var taskType = NormalizeWriteTaskType(type, request.TaskType);
        var localPath = taskType == FileTaskType.Delete
            ? NormalizeLocalPathForDelete(request.LocalPath)
            : NormalizeLocalFilePath(request.LocalPath);
        var targetPath = NormalizeFilePath(request.TargetPath);

        return new WriteTask
        {
            TaskType = taskType,
            LocalPath = localPath,
            TargetPath = targetPath,
            LtfsPath = taskType == FileTaskType.Delete
                ? LtfsFile.Default()
                : CreateLtfsFile(localPath, targetPath),
        };
    }

    private static WriteTask CreateDefaultWriteTask(string type)
    {
        var taskType = type switch
        {
            LtfsTaskType.Write => FileTaskType.Write,
            LtfsTaskType.Replace => FileTaskType.Replace,
            LtfsTaskType.Delete => FileTaskType.Delete,
            _ => FileTaskType.Write,
        };

        return new WriteTask
        {
            TaskType = taskType,
            LocalPath = string.Empty,
            TargetPath = string.Empty,
            LtfsPath = Ltfs.Index.LtfsFile.Default(),
        };
    }

    private static IEnumerable<LtfsTaskItem> BuildServerFolderTasks(string tapeBarcode, string localRootPath, string targetRootPath)
    {
        var queue = new Queue<(string LocalDir, string TapeDir)>();
        queue.Enqueue((localRootPath, targetRootPath));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            yield return new LtfsTaskItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = LtfsTaskType.Folder,
                TapeBarcode = tapeBarcode,
                FolderTask = new FolderTask
                {
                    TaskType = FolderTaskType.Add,
                    Path = current.TapeDir,
                },
                CreatedAtTicks = DateTime.UtcNow.Ticks,
            };

            foreach (var filePath in Directory.EnumerateFiles(current.LocalDir).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileName(filePath);
                var targetPath = CombineTapePath(current.TapeDir, fileName);

                yield return new LtfsTaskItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Type = LtfsTaskType.Write,
                    TapeBarcode = tapeBarcode,
                    WriteTask = new WriteTask
                    {
                        TaskType = FileTaskType.Write,
                        LocalPath = filePath,
                        TargetPath = targetPath,
                        LtfsPath = CreateLtfsFile(filePath, targetPath),
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

    private LtfsTaskGroup GetOrCreateGroupCore(string tapeBarcode)
    {
        if (_groups.TryGetValue(tapeBarcode, out var existing))
        {
            return existing;
        }

        var now = DateTime.UtcNow.Ticks;
        var group = new LtfsTaskGroup
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

    private static void EnsureNoFormatTask(LtfsTaskGroup group)
    {
        if (group.Tasks.Any(t => string.Equals(t.Type, LtfsTaskType.Format, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Format task already exists for this tape.");
        }
    }

    private static string NormalizeBarcode(string tapeBarcode)
    {
        var normalized = (tapeBarcode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Tape barcode is required.");
        }

        return normalized;
    }

    private static string NormalizeTaskType(string type)
    {
        var normalized = (type ?? string.Empty).Trim().ToLowerInvariant();
        if (!LtfsTaskType.IsValid(normalized))
        {
            throw new ArgumentException($"Unsupported task type '{type}'.");
        }

        return normalized;
    }

    private static string NormalizeFolderTaskType(string type)
    {
        var normalized = (type ?? string.Empty).Trim().ToLowerInvariant();
        if (!FolderTaskType.IsValid(normalized))
        {
            throw new ArgumentException($"Unsupported folder task type '{type}'.");
        }

        return normalized;
    }

    private static string NormalizeFolderPath(string path)
    {
        var normalized = (path ?? string.Empty).Trim().Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Folder path is required.");
        }

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
        {
            throw new ArgumentException("File target path cannot be root.");
        }

        return normalized;
    }

    private static string NormalizeLocalDirectoryPath(string path)
    {
        var trimmed = (path ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Local directory path is required.");
        }

        var normalized = Path.GetFullPath(trimmed);
        if (!Directory.Exists(normalized))
        {
            throw new DirectoryNotFoundException($"Directory '{trimmed}' was not found.");
        }

        return normalized;
    }

    private static string NormalizeLocalFilePath(string path)
    {
        var trimmed = (path ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Local file path is required.");
        }

        var normalized = Path.GetFullPath(trimmed);
        if (!File.Exists(normalized))
        {
            throw new FileNotFoundException($"File '{trimmed}' was not found.", normalized);
        }

        return normalized;
    }

    private static string NormalizeLocalPathForDelete(string path)
    {
        var trimmed = (path ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? string.Empty : Path.GetFullPath(trimmed);
    }

    private static FileTaskType NormalizeWriteTaskType(string type, string? requestTaskType)
    {
        var expected = type switch
        {
            LtfsTaskType.Write => FileTaskType.Write,
            LtfsTaskType.Replace => FileTaskType.Replace,
            LtfsTaskType.Delete => FileTaskType.Delete,
            _ => throw new ArgumentException($"Unsupported write task type '{type}'."),
        };

        if (string.IsNullOrWhiteSpace(requestTaskType))
        {
            return expected;
        }

        if (!Enum.TryParse<FileTaskType>(requestTaskType.Trim(), true, out var parsed) || parsed != expected)
        {
            throw new ArgumentException($"Write taskType '{requestTaskType}' does not match task type '{type}'.");
        }

        return expected;
    }

    private static LtfsFile CreateLtfsFile(string localPath, string targetPath)
    {
        var fileInfo = new FileInfo(localPath);
        var ltfsFile = LtfsFile.Default();
        ltfsFile.Name = Path.GetFileName(targetPath);
        ltfsFile.Length = (ulong)fileInfo.Length;
        ltfsFile.CreationTime = fileInfo.CreationTimeUtc;
        ltfsFile.ChangeTime = fileInfo.LastWriteTimeUtc;
        ltfsFile.ModifyTime = fileInfo.LastWriteTimeUtc;
        ltfsFile.AccessTime = fileInfo.LastAccessTimeUtc;
        ltfsFile.BackupTime = fileInfo.LastWriteTimeUtc;
        ltfsFile.ReadOnly = fileInfo.IsReadOnly;
        return ltfsFile;
    }

    private static string CombineTapePath(string parentPath, string childName)
    {
        var normalizedParent = NormalizeFolderPath(parentPath);
        var trimmedChild = (childName ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(trimmedChild))
        {
            throw new ArgumentException("Path segment is required.");
        }

        return normalizedParent == "/" ? "/" + trimmedChild : normalizedParent + "/" + trimmedChild;
    }

    private static void ValidateGroup(LtfsTaskGroup group)
    {
        var formatIndexes = group.Tasks
            .Select((task, index) => new { task, index })
            .Where(entry => string.Equals(entry.task.Type, LtfsTaskType.Format, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.index)
            .ToArray();

        if (formatIndexes.Length > 1)
        {
            throw new InvalidOperationException("Format task must be unique in a tape task group.");
        }

        if (formatIndexes.Length == 1 && formatIndexes[0] != 0)
        {
            throw new InvalidOperationException("Format task must be the first task in the tape task group.");
        }

        foreach (var task in group.Tasks)
        {
            if (!string.Equals(task.TapeBarcode, group.TapeBarcode, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Task barcode must match task group barcode.");
            }

            if (!LtfsTaskType.IsValid(task.Type))
            {
                throw new InvalidOperationException($"Unsupported task type '{task.Type}'.");
            }

            if (task.Type == LtfsTaskType.Folder)
            {
                if (task.FolderTask is null)
                {
                    throw new InvalidOperationException("Folder task payload is required.");
                }

                task.FolderTask.TaskType = NormalizeFolderTaskType(task.FolderTask.TaskType);
                task.FolderTask.Path = NormalizeFolderPath(task.FolderTask.Path);
            }
        }
    }

    private void LoadFromDisk()
    {
        lock (_syncRoot)
        {
            if (!File.Exists(_storePath))
            {
                return;
            }

            var json = File.ReadAllText(_storePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var groups = JsonSerializer.Deserialize<List<LtfsTaskGroup>>(json);
            if (groups is null)
            {
                return;
            }

            _groups.Clear();
            foreach (var group in groups)
            {
                if (string.IsNullOrWhiteSpace(group.TapeBarcode))
                {
                    continue;
                }

                group.TapeBarcode = NormalizeBarcode(group.TapeBarcode);
                group.Name = string.IsNullOrWhiteSpace(group.Name) ? group.TapeBarcode : group.Name.Trim();
                group.Tasks ??= [];
                foreach (var task in group.Tasks)
                {
                    task.Type = NormalizeTaskType(task.Type);
                    task.TapeBarcode = group.TapeBarcode;
                    task.Id = string.IsNullOrWhiteSpace(task.Id) ? Guid.NewGuid().ToString("N") : task.Id.Trim();
                    task.CreatedAtTicks = task.CreatedAtTicks == 0 ? DateTime.UtcNow.Ticks : task.CreatedAtTicks;

                    if (task.Type is LtfsTaskType.Write or LtfsTaskType.Replace or LtfsTaskType.Delete)
                    {
                        task.WriteTask ??= CreateDefaultWriteTask(task.Type);
                        task.WriteTask.TaskType = task.Type switch
                        {
                            LtfsTaskType.Write => FileTaskType.Write,
                            LtfsTaskType.Replace => FileTaskType.Replace,
                            LtfsTaskType.Delete => FileTaskType.Delete,
                            _ => task.WriteTask.TaskType,
                        };
                    }

                    if (task.Type == LtfsTaskType.Read)
                    {
                        task.ReadTask ??= new ReadTask();
                    }

                    if (task.Type == LtfsTaskType.Format)
                    {
                        task.FormatTask ??= new FormatTask
                        {
                            FormatParam = new FormatParam(),
                        };
                        if (string.IsNullOrWhiteSpace(task.FormatTask.FormatParam.Barcode))
                        {
                            task.FormatTask.FormatParam.Barcode = group.TapeBarcode;
                        }
                    }

                    if (task.Type == LtfsTaskType.Folder)
                    {
                        task.FolderTask ??= new FolderTask();
                        task.FolderTask.TaskType = NormalizeFolderTaskType(task.FolderTask.TaskType);
                        task.FolderTask.Path = NormalizeFolderPath(task.FolderTask.Path);
                    }
                }

                group.UpdatedAtTicks = group.UpdatedAtTicks == 0 ? DateTime.UtcNow.Ticks : group.UpdatedAtTicks;
                ValidateGroup(group);
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

    private static LtfsTaskGroup Clone(LtfsTaskGroup group)
    {
        var json = JsonSerializer.Serialize(group);
        var cloned = JsonSerializer.Deserialize<LtfsTaskGroup>(json);
        return cloned ?? new LtfsTaskGroup
        {
            TapeBarcode = group.TapeBarcode,
            Name = group.Name,
            Tasks = [],
            UpdatedAtTicks = group.UpdatedAtTicks,
        };
    }
}
