using Ltfs.Index;
using Ltfs.Tasks;

namespace Ltfs;

internal static class LtfsIndexOperations
{
    public static string NormalizePath(string path, bool allowRoot = true)
    {
        var normalized = (path ?? string.Empty).Trim().Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            if (allowRoot)
                return "/";

            throw new ArgumentException("Path is required.", nameof(path));
        }

        normalized = normalized == "/" ? "/" : "/" + normalized.Trim('/');
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        if (!allowRoot && normalized == "/")
            throw new ArgumentException("Root path is not allowed.", nameof(path));

        return normalized;
    }

    public static object? FindEntry(LtfsIndex? index, string path)
    {
        if (index?.Directory is null)
            return null;

        var normalizedPath = NormalizePath(path);
        if (normalizedPath == "/")
            return index.Directory;

        var parts = SplitPath(normalizedPath);
        LtfsDirectory current = index.Directory;
        for (int i = 0; i < parts.Length; i++)
        {
            var entry = current[parts[i]];
            if (i == parts.Length - 1)
                return entry;

            if (entry is not LtfsDirectory subDirectory)
                return null;

            current = subDirectory;
        }

        return null;
    }

    public static LtfsFile? FindFile(LtfsIndex? index, string path) => FindEntry(index, path) as LtfsFile;

    public static LtfsDirectory? FindDirectory(LtfsIndex? index, string path) => FindEntry(index, path) as LtfsDirectory;

    public static void ApplyTask(LtfsIndex index, TaskBase task)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(task);

        switch (task)
        {
            case CreateDirectoryTask createDirectoryTask:
                EnsureDirectory(index, createDirectoryTask.TargetPath);
                break;
            case DeleteTask deleteTask:
                DeletePath(index, deleteTask.TargetPath);
                break;
            case MoveTask moveTask:
                MovePath(index, moveTask.SourcePath, moveTask.TargetPath);
                break;
            case WriteTask writeTask:
                UpsertFile(index, writeTask.TargetPath, writeTask.LtfsTargetPath);
                break;
        }

        index.UpdateTime = DateTime.UtcNow;
    }

    public static LtfsDirectory EnsureDirectory(LtfsIndex index, string path)
    {
        ArgumentNullException.ThrowIfNull(index);

        var normalizedPath = NormalizePath(path);
        if (normalizedPath == "/")
            return index.Directory;

        var current = index.Directory;
        foreach (var part in SplitPath(normalizedPath))
        {
            var existing = current[part];
            if (existing is LtfsDirectory directory)
            {
                current = directory;
                continue;
            }

            if (existing is LtfsFile)
                throw new InvalidOperationException($"Path '{normalizedPath}' conflicts with an existing file.");

            current = CreateDirectory(index, current, part);
        }

        return current;
    }

    public static void UpsertFile(LtfsIndex index, string path, LtfsFile file)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(file);

        var normalizedPath = NormalizePath(path, allowRoot: false);
        var parent = EnsureDirectory(index, GetParentPath(normalizedPath));
        var fileName = GetLeafName(normalizedPath);

        if (parent[fileName] is LtfsDirectory)
            throw new InvalidOperationException($"Path '{normalizedPath}' conflicts with an existing directory.");

        if (file.FileUID == 0)
        {
            index.HighestFileUID += 1;
            file.FileUID = index.HighestFileUID;
        }

        file.Name = fileName;
        file.ChangeTime = DateTime.UtcNow;
        parent[fileName] = file;
        Touch(parent);
    }

    public static void DeletePath(LtfsIndex index, string path)
    {
        ArgumentNullException.ThrowIfNull(index);

        var normalizedPath = NormalizePath(path);
        if (normalizedPath == "/")
        {
            index.Directory.RemoveAll();
            Touch(index.Directory);
            return;
        }

        var parent = FindDirectory(index, GetParentPath(normalizedPath));
        if (parent is null)
            return;

        RemoveChild(parent, GetLeafName(normalizedPath));
        Touch(parent);
    }

    public static void MovePath(LtfsIndex index, string sourcePath, string targetPath)
    {
        ArgumentNullException.ThrowIfNull(index);

        var normalizedSource = NormalizePath(sourcePath, allowRoot: false);
        var normalizedTarget = NormalizePath(targetPath, allowRoot: false);
        if (string.Equals(normalizedSource, normalizedTarget, StringComparison.OrdinalIgnoreCase))
            return;

        var sourceParent = FindDirectory(index, GetParentPath(normalizedSource));
        if (sourceParent is null)
            return;

        var entry = DetachChild(sourceParent, GetLeafName(normalizedSource));
        if (entry is null)
            return;

        var targetParent = EnsureDirectory(index, GetParentPath(normalizedTarget));
        var targetName = GetLeafName(normalizedTarget);

        if (entry is LtfsDirectory directory)
        {
            directory.Name = targetName;
            targetParent[targetName] = directory;
        }
        else if (entry is LtfsFile file)
        {
            file.Name = targetName;
            file.ChangeTime = DateTime.UtcNow;
            targetParent[targetName] = file;
        }

        Touch(sourceParent);
        Touch(targetParent);
    }

    private static LtfsDirectory CreateDirectory(LtfsIndex index, LtfsDirectory parent, string name)
    {
        index.HighestFileUID += 1;
        var directory = new LtfsDirectory
        {
            Name = name,
            FileUID = (uint)index.HighestFileUID,
            CreationTime = DateTime.UtcNow,
            ChangeTime = DateTime.UtcNow,
            ModifyTime = DateTime.UtcNow,
            AccessTime = DateTime.UtcNow,
            BackupTime = DateTime.UtcNow,
            ReadOnly = false,
            Contents = Array.Empty<object>(),
        };

        parent[name] = directory;
        Touch(parent);
        return directory;
    }

    private static object? DetachChild(LtfsDirectory directory, string name)
    {
        object? removed = null;
        directory.Contents = directory.Contents.Where(entry =>
        {
            var matches = entry switch
            {
                LtfsFile file => string.Equals(file.Name.GetName(), name, StringComparison.OrdinalIgnoreCase),
                LtfsDirectory subDirectory => string.Equals(subDirectory.Name.GetName(), name, StringComparison.OrdinalIgnoreCase),
                _ => false,
            };

            if (matches)
                removed = entry;

            return !matches;
        }).ToArray();

        return removed;
    }

    private static void RemoveChild(LtfsDirectory directory, string name)
    {
        directory.Contents = directory.Contents.Where(entry => entry switch
        {
            LtfsFile file => !string.Equals(file.Name.GetName(), name, StringComparison.OrdinalIgnoreCase),
            LtfsDirectory subDirectory => !string.Equals(subDirectory.Name.GetName(), name, StringComparison.OrdinalIgnoreCase),
            _ => true,
        }).ToArray();
    }

    private static string[] SplitPath(string normalizedPath)
    {
        return normalizedPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string GetParentPath(string normalizedPath)
    {
        var parts = SplitPath(normalizedPath);
        if (parts.Length <= 1)
            return "/";

        return "/" + string.Join('/', parts[..^1]);
    }

    private static string GetLeafName(string normalizedPath)
    {
        var parts = SplitPath(normalizedPath);
        if (parts.Length == 0)
            throw new ArgumentException("Path does not contain a leaf name.", nameof(normalizedPath));

        return parts[^1];
    }

    private static void Touch(LtfsDirectory directory)
    {
        var now = DateTime.UtcNow;
        directory.ChangeTime = now;
        directory.ModifyTime = now;
        directory.AccessTime = now;
    }
}