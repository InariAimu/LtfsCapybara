using System.Globalization;
using Ltfs;
using Ltfs.Index;
using LtfsServer.BootStrap;
using LtfsServer.Features.LocalTapes;
using LtfsServer.Features.Tasks;

namespace LtfsServer.Features.LocalIndex;

public interface ILocalIndexQueryService
{
    LocalIndexQueryResponse GetDirectory(string tapeName, string requestedPath);
}

public sealed class LocalIndexQueryService : ILocalIndexQueryService
{
    private readonly ILocalTapeRegistry _registry;
    private readonly ITaskGroupService _taskService;
    private readonly AppData _appData;

    public LocalIndexQueryService(ILocalTapeRegistry registry, ITaskGroupService taskService, AppData appData)
    {
        _registry = registry;
        _taskService = taskService;
        _appData = appData;
    }

    public LocalIndexQueryResponse GetDirectory(string tapeName, string requestedPath)
    {
        var normalizedPath = LocalIndexPath.NormalizePath(requestedPath);
        var file = _registry.GetFiles(tapeName)
            .Where(HasXmlIndex)
            .OrderByDescending(f => f.Index.Ticks)
            .FirstOrDefault();

        LtfsDirectory? root = null;
        if (file is not null)
        {
            var indexPath = Path.Combine(_appData.Path, "local", tapeName, file.Index.FileName);
            var index = LtfsIndex.FromXmlFile(indexPath);
            if (index is null)
            {
                return new LocalIndexQueryResponse(StatusCodes.Status500InternalServerError, new { error = "Failed to load LTFS index" });
            }

            root = index.Directory;
        }

        var taskGroup = _taskService.ListGroups()
            .FirstOrDefault(g => string.Equals(g.TapeBarcode, tapeName, StringComparison.OrdinalIgnoreCase));
        var overlayState = LocalIndexOverlay.BuildTaskOverlayState(taskGroup);

        if (root is null && !overlayState.HasTasks)
        {
            return new LocalIndexQueryResponse(StatusCodes.Status404NotFound, new { error = "No index files found for tape" });
        }

        var target = root is null ? null : LocalIndexPath.FindDirectoryByPath(root, normalizedPath);
        if (target is null && normalizedPath != "/" && !LocalIndexPath.CanResolveTaskPath(normalizedPath, overlayState))
        {
            return new LocalIndexQueryResponse(StatusCodes.Status404NotFound, new { error = "Path not found" });
        }

        var dto = BuildDirectoryDto(target, normalizedPath, overlayState, tapeName);
        return new LocalIndexQueryResponse(StatusCodes.Status200OK, dto);
    }

    private static bool HasXmlIndex(TapeFileInfo file)
    {
        return file.Index.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
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
                        Size = 0,
                    };
                }

                existing.Task = "delete";
                itemMap[childName] = existing;
                continue;
            }

            var dirItem = itemMap.TryGetValue(childName, out var current)
                ? current
                : new LocalIndexItemDto
                {
                    Type = "dir",
                    Name = childName,
                    Size = 0,
                };
            dirItem.Type = "dir";
            dirItem.Task = "add";
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
                        Size = 0,
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
                    ModifyTime = string.Empty,
                };
            }

            fileItem.Type = "file";
            fileItem.Task = action.Value;
            itemMap[childName] = fileItem;
        }

        if (IsDirectoryOrAncestorDeleted(normalizedPath, overlayState.FolderActions))
        {
            foreach (var item in itemMap.Values)
            {
                item.Task = "delete";
            }
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

    private static bool IsDirectoryOrAncestorDeleted(string normalizedPath, IReadOnlyDictionary<string, string> folderActions)
    {
        var path = normalizedPath;
        while (true)
        {
            if (folderActions.TryGetValue(path, out var action) && action == "delete")
                return true;
            if (path == "/") break;
            var lastSlash = path.LastIndexOf('/');
            path = lastSlash <= 0 ? "/" : path[..lastSlash];
        }
        return false;
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
                    Crc64 = f.ExtendedAttributes?["ltfs.hash.crc64sum"] ?? string.Empty,
                    ModifyTime = ((DateTime)f.ModifyTime).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                };
                continue;
            }

            if (item is LtfsDirectory d)
            {
                yield return new LocalIndexItemDto
                {
                    Type = "dir",
                    Name = d.Name.GetName(),
                    Size = d.Count,
                    Index = d.FileUID,
                };
            }
        }
    }
}

public sealed record LocalIndexQueryResponse(int StatusCode, object Payload);

public sealed class LocalIndexDirectoryDto
{
    public string Name { get; set; } = string.Empty;
    public LocalIndexItemDto[] Items { get; set; } = [];
}

public sealed class LocalIndexItemDto
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Task { get; set; }
    public object? Size { get; set; }
    public object? Index { get; set; }
    public string? Crc64 { get; set; }
    public string? ModifyTime { get; set; }
}