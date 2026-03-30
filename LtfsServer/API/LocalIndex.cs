using Microsoft.AspNetCore.Builder;
using LtfsServer.Services;
using Ltfs;
using Ltfs.Index;
using LtoTape;

namespace LtfsServer.API;

public static class APILocalIndex
{
    public static void MapLocalIndexApi(this WebApplication app)
    {
        app.MapGet("/api/local/{tapeName}", (string tapeName, ILocalTapeRegistry registry, ITaskGroupService taskService, AppData appData) =>
        {
            return GetLocalDirectoryDto(tapeName, "/", registry, taskService, appData);
        });

        app.MapGet("/api/local/{tapeName}/{**path}", (string tapeName, string path, ILocalTapeRegistry registry, ITaskGroupService taskService, AppData appData) =>
        {
            return GetLocalDirectoryDto(tapeName, path, registry, taskService, appData);
        });

        app.MapGet("/api/localcm/{tapeName}", (string tapeName, ILocalTapeRegistry registry, AppData appData) =>
        {
            var file = registry.GetFiles(tapeName)
                .Where(HasCartridgeMemory)
                .OrderByDescending(f => f.Index.Ticks)
                .FirstOrDefault();

            if (file is null)
                return Results.NotFound(new { error = "No cartridge memory files found for tape" });

            var cmPath = Path.Combine(appData.Path, "local", tapeName, file.Index.FileName);
            if (!File.Exists(cmPath))
                return Results.NotFound(new { error = "Cartridge memory file not found" });

            try
            {
                var cartridgeMemory = new CartridgeMemory();
                if (cmPath.EndsWith(".cmbin", StringComparison.OrdinalIgnoreCase))
                    cartridgeMemory.FromBinaryFile(cmPath);
                else
                    cartridgeMemory.FromLcgCmFile(cmPath);
                return Results.Ok(cartridgeMemory);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    title: "Failed to parse cartridge memory file",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }

    private static bool HasXmlIndex(TapeFileInfo file)
    {
        return file.Index.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCartridgeMemory(TapeFileInfo file)
    {
        return file.Index.FileName.EndsWith(".cm", StringComparison.OrdinalIgnoreCase)
            || file.Index.FileName.EndsWith(".cmbin", StringComparison.OrdinalIgnoreCase);
    }

    private static IResult GetLocalDirectoryDto(
        string tapeName,
        string requestedPath,
        ILocalTapeRegistry registry,
        ITaskGroupService taskService,
        AppData appData)
    {
        var normalizedPath = NormalizePath(requestedPath);
        var files = registry.GetFiles(tapeName)
            .Where(HasXmlIndex)
            .OrderByDescending(f => f.Index.Ticks)
            .ToArray();

        LtfsDirectory? root = null;
        if (files.Length > 0)
        {
            var file = files[0];
            var indexPath = Path.Combine(appData.Path, "local", tapeName, file.Index.FileName);
            var index = LtfsIndex.FromXmlFile(indexPath);
            if (index is null)
            {
                return Results.StatusCode(500);
            }

            root = index.Directory;
        }

        var taskGroup = taskService.ListGroups()
            .FirstOrDefault(g => string.Equals(g.TapeBarcode, tapeName, StringComparison.OrdinalIgnoreCase));
        var hasTasks = taskGroup?.Tasks?.Count > 0;
        var hasFormatTask = taskGroup?.Tasks?.Any(t =>
            string.Equals(t.Type, LtfsTaskType.Format, StringComparison.OrdinalIgnoreCase)) == true;

        if (root is null && !hasTasks)
        {
            return Results.NotFound(new { error = "No index files found for tape" });
        }

        var target = root is null ? null : FindDirectoryByPath(root, normalizedPath);
        if (target is null && root is not null && normalizedPath != "/" && !CanResolveTaskPath(normalizedPath, taskGroup))
        {
            return Results.NotFound(new { error = "Path not found" });
        }

        if (target is null && root is null && normalizedPath != "/" && !CanResolveTaskPath(normalizedPath, taskGroup))
        {
            return Results.NotFound(new { error = "Path not found" });
        }

        if (target is null && root is null && normalizedPath == "/" && !hasFormatTask && !CanResolveTaskPath("/", taskGroup))
        {
            return Results.NotFound(new { error = "No index files found for tape" });
        }

        var dto = BuildDirectoryDto(target, normalizedPath, taskGroup);
        return Results.Ok(dto);
    }

    private static LocalIndexDirectoryDto BuildDirectoryDto(
        LtfsDirectory? directory,
        string normalizedPath,
        LtfsTaskGroup? taskGroup)
    {
        var itemMap = new Dictionary<string, LocalIndexItemDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in EnumerateIndexItems(directory))
        {
            itemMap[item.Name] = item;
        }

        var folderActions = BuildFolderActionMap(taskGroup);
        foreach (var action in folderActions)
        {
            var childName = GetDirectChildName(normalizedPath, action.Key);
            if (string.IsNullOrWhiteSpace(childName))
            {
                continue;
            }

            if (action.Value == "delete")
            {
                if (!itemMap.TryGetValue(childName, out var existing))
                {
                    existing = new LocalIndexItemDto
                    {
                        Type = "dir",
                        Name = childName,
                        Count = 0,
                    };
                }

                existing.TaskType = "delete";
                itemMap[childName] = existing;
                continue;
            }

            var dirItem = itemMap.TryGetValue(childName, out var current)
                ? current
                : new LocalIndexItemDto
                {
                    Type = "dir",
                    Name = childName,
                    Count = 0,
                };
            dirItem.Type = "dir";
            dirItem.TaskType = "add";
            itemMap[childName] = dirItem;
        }

        var fileActions = BuildFileActionMap(taskGroup);
        foreach (var action in fileActions)
        {
            var childName = GetDirectChildName(normalizedPath, action.Key);
            if (string.IsNullOrWhiteSpace(childName))
            {
                continue;
            }

            if (!itemMap.TryGetValue(childName, out var fileItem))
            {
                fileItem = new LocalIndexItemDto
                {
                    Type = "file",
                    Name = childName,
                    Size = 0,
                    Crc64 = string.Empty,
                    CreateTime = string.Empty,
                    ModifyTime = string.Empty,
                    UpdateTime = string.Empty,
                    BackupTime = string.Empty,
                };
            }

            fileItem.Type = "file";
            fileItem.TaskType = action.Value;
            itemMap[childName] = fileItem;
        }

        var directoryName = directory?.Name.GetName() ?? (normalizedPath == "/" ? string.Empty : normalizedPath.Trim('/').Split('/').LastOrDefault() ?? string.Empty);
        return new LocalIndexDirectoryDto
        {
            Name = directoryName,
            Items = itemMap.Values
                .OrderBy(i => i.Type == "dir" ? 0 : 1)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
        };
    }

    private static IEnumerable<LocalIndexItemDto> EnumerateIndexItems(LtfsDirectory? directory)
    {
        if (directory is null)
        {
            yield break;
        }

        foreach (var item in directory.Contents)
        {
            if (item is LtfsFile f)
            {
                yield return new LocalIndexItemDto
                {
                    Type = "file",
                    Name = f.Name.GetName(),
                    Size = f.Length,
                    Index = f.FileUID,
                    Crc64 = f.ExtendedAttributes?["ltfs.hash.sha1sum"] ?? string.Empty,
                    CreateTime = f.CreationTime.ToString(),
                    ModifyTime = f.ModifyTime.ToString(),
                    UpdateTime = f.ChangeTime.ToString(),
                    BackupTime = f.BackupTime.ToString(),
                };
                continue;
            }

            if (item is LtfsDirectory d)
            {
                yield return new LocalIndexItemDto
                {
                    Type = "dir",
                    Name = d.Name.GetName(),
                    Index = d.FileUID,
                    Count = d.Count,
                };
            }
        }
    }

    private static Dictionary<string, string> BuildFolderActionMap(LtfsTaskGroup? taskGroup)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (taskGroup?.Tasks is null)
        {
            return map;
        }

        foreach (var task in taskGroup.Tasks.OrderBy(t => t.CreatedAtTicks))
        {
            if (!string.Equals(task.Type, LtfsTaskType.Folder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var folderTask = task.FolderTask;
            if (folderTask is null)
            {
                continue;
            }

            var path = NormalizePath(folderTask.Path);
            var taskType = (folderTask.TaskType ?? string.Empty).Trim().ToLowerInvariant();
            if (taskType is "add" or "delete")
            {
                map[path] = taskType;
            }
        }

        return map;
    }

    private static Dictionary<string, string> BuildFileActionMap(LtfsTaskGroup? taskGroup)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (taskGroup?.Tasks is null)
        {
            return map;
        }

        foreach (var task in taskGroup.Tasks.OrderBy(t => t.CreatedAtTicks))
        {
            if (task.WriteTask is null)
            {
                continue;
            }

            string? action = task.Type.ToLowerInvariant() switch
            {
                LtfsTaskType.Write => "add",
                LtfsTaskType.Replace => "replace",
                LtfsTaskType.Delete => "delete",
                _ => null,
            };

            if (action is null)
            {
                continue;
            }

            var targetPath = NormalizePath(task.WriteTask.TargetPath);
            if (targetPath == "/")
            {
                continue;
            }

            map[targetPath] = action;
        }

        return map;
    }

    private static bool CanResolveTaskPath(string normalizedPath, LtfsTaskGroup? taskGroup)
    {
        if (normalizedPath == "/")
        {
            return true;
        }

        var folderActions = BuildFolderActionMap(taskGroup);
        if (folderActions.TryGetValue(normalizedPath, out var action) && action != "delete")
        {
            return true;
        }

        var prefix = normalizedPath == "/" ? "/" : normalizedPath + "/";
        if (folderActions.Any(kvp => kvp.Value != "delete" && kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var fileActions = BuildFileActionMap(taskGroup);
        if (fileActions.Any(kvp => kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static string? GetDirectChildName(string parentPath, string candidatePath)
    {
        var normalizedParent = NormalizePath(parentPath);
        var normalizedCandidate = NormalizePath(candidatePath);
        if (normalizedCandidate == normalizedParent)
        {
            return null;
        }

        var parentPrefix = normalizedParent == "/" ? "/" : normalizedParent + "/";
        if (!normalizedCandidate.StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var remain = normalizedCandidate[parentPrefix.Length..];
        if (string.IsNullOrWhiteSpace(remain))
        {
            return null;
        }

        var segment = remain.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(segment) ? null : segment;
    }

    private static string NormalizePath(string path)
    {
        var normalized = (path ?? string.Empty).Trim().Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized == "/")
        {
            return "/";
        }

        normalized = normalized.Trim('/');
        normalized = "/" + normalized;
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        return normalized;
    }

    private static LtfsDirectory? FindDirectoryByPath(LtfsDirectory root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/") return root;

        // normalize and split
        var trimmed = path.Trim('/');
        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);

        LtfsDirectory? curr = root;
        foreach (var seg in segments)
        {
            if (curr is null) return null;
            object? next = curr[seg];
            if (next is LtfsDirectory d)
            {
                curr = d;
                continue;
            }
            else
            {
                return null;
            }
        }

        return curr;
    }

    private sealed class LocalIndexDirectoryDto
    {
        public string Name { get; set; } = string.Empty;
        public LocalIndexItemDto[] Items { get; set; } = [];
    }

    private sealed class LocalIndexItemDto
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? TaskType { get; set; }
        public object? Size { get; set; }
        public object? Index { get; set; }
        public string? Crc64 { get; set; }
        public string? CreateTime { get; set; }
        public string? ModifyTime { get; set; }
        public string? UpdateTime { get; set; }
        public string? BackupTime { get; set; }
        public int? Count { get; set; }
    }
}
