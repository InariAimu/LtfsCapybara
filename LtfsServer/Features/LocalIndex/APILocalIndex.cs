using Microsoft.AspNetCore.Builder;
using Ltfs;
using Ltfs.Index;
using LtoTape;
using LtfsServer.Features.LocalTapes;
using LtfsServer.Features.Tasks;
using LtfsServer.BootStrap;

namespace LtfsServer.Features.LocalIndex;

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
        var normalizedPath = LocalIndexPath.NormalizePath(requestedPath);
        var file = registry.GetFiles(tapeName)
            .Where(HasXmlIndex)
            .OrderByDescending(f => f.Index.Ticks)
            .FirstOrDefault();

        LtfsDirectory? root = null;
        if (file is not null)
        {
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
        var overlayState = LocalIndexOverlay.BuildTaskOverlayState(taskGroup);

        if (root is null && !overlayState.HasTasks)
        {
            return Results.NotFound(new { error = "No index files found for tape" });
        }

        var target = root is null ? null : LocalIndexPath.FindDirectoryByPath(root, normalizedPath);
        if (target is null && normalizedPath != "/" && !LocalIndexPath.CanResolveTaskPath(normalizedPath, overlayState))
        {
            return Results.NotFound(new { error = "Path not found" });
        }

        var dto = BuildDirectoryDto(target, normalizedPath, overlayState, tapeName);
        return Results.Ok(dto);
    }

    private static LocalIndexDirectoryDto BuildDirectoryDto(
        LtfsDirectory? directory,
        string normalizedPath,
        TaskOverlayState overlayState,
        string tapeName)
    {
        var itemMap = new Dictionary<string, LocalIndexItemDto>(StringComparer.OrdinalIgnoreCase);
        var parentPrefix = normalizedPath == "/" ? "/" : normalizedPath + "/";

        foreach (var item in EnumerateIndexItems(directory))
        {
            itemMap[item.Name] = item;
        }

        foreach (var action in overlayState.FolderActions)
        {
            var childName = LocalIndexPath.GetDirectChildName(normalizedPath, parentPrefix, action.Key);
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

        foreach (var action in overlayState.FileActions)
        {
            var childName = LocalIndexPath.GetDirectChildName(normalizedPath, parentPrefix, action.Key);
            if (string.IsNullOrWhiteSpace(childName))
            {
                continue;
            }

            var isDirectChildFile = LocalIndexPath.IsDirectChildPath(normalizedPath, parentPrefix, action.Key);

            if (!isDirectChildFile)
            {
                var dirItem = itemMap.TryGetValue(childName, out var current)
                    ? current
                    : new LocalIndexItemDto
                    {
                        Type = "dir",
                        Name = childName,
                        Count = 0,
                    };

                dirItem.Type = "dir";
                itemMap[childName] = dirItem;
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

        var directoryName = directory?.Name.GetName() ??
            (normalizedPath == "/"
                ? tapeName
                : normalizedPath.Trim('/').Split('/').LastOrDefault() ?? string.Empty);

        if (normalizedPath == "/" && string.IsNullOrWhiteSpace(directoryName))
        {
            directoryName = tapeName;
        }

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
