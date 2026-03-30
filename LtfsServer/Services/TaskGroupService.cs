using System.Text.Json;
using Ltfs;

namespace LtfsServer.Services;

public interface ITaskGroupService
{
    IReadOnlyList<LtfsTaskGroup> ListGroups();
    LtfsTaskGroup GetOrCreateGroup(string tapeBarcode);
    LtfsTaskGroup RenameGroup(string tapeBarcode, string name);
    LtfsTaskGroup AddTask(string tapeBarcode, LtfsTaskCreateRequest request);
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

    public static bool IsValid(string type)
    {
        return type is Write or Replace or Delete or Read or Format;
    }
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
    public long CreatedAtTicks { get; set; } = DateTime.UtcNow.Ticks;
}

public sealed class LtfsTaskCreateRequest
{
    public string Type { get; set; } = string.Empty;
    public string TapeBarcode { get; set; } = string.Empty;
    public WriteTask? WriteTask { get; set; }
    public ReadTask? ReadTask { get; set; }
    public FormatTask? FormatTask { get; set; }
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
            var writeTask = request.WriteTask ?? CreateDefaultWriteTask(type);
            writeTask.TaskType = type switch
            {
                LtfsTaskType.Write => FileTaskType.Write,
                LtfsTaskType.Replace => FileTaskType.Replace,
                LtfsTaskType.Delete => FileTaskType.Delete,
                _ => writeTask.TaskType,
            };
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

        throw new ArgumentException($"Unsupported task type '{type}'.");
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
