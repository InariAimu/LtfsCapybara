using Ltfs.Index;

namespace LtfsServer.API;

internal static class LocalIndexPath
{
    public static bool CanResolveTaskPath(string normalizedPath, TaskOverlayState overlayState)
    {
        if (normalizedPath == "/")
        {
            return overlayState.HasTasks || overlayState.HasFormatTask;
        }

        var folderActions = overlayState.FolderActions;
        if (folderActions.TryGetValue(normalizedPath, out var action) && action != "delete")
        {
            return true;
        }

        var prefix = normalizedPath == "/" ? "/" : normalizedPath + "/";
        if (folderActions.Any(kvp => kvp.Value != "delete" && kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var fileActions = overlayState.FileActions;
        if (fileActions.Any(kvp => kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    public static string? GetDirectChildName(string normalizedParentPath, string parentPrefix, string candidatePath)
    {
        var normalizedCandidate = NormalizePath(candidatePath);
        if (normalizedCandidate == normalizedParentPath)
        {
            return null;
        }

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

    public static bool IsDirectChildPath(string normalizedParentPath, string parentPrefix, string candidatePath)
    {
        var normalizedCandidate = NormalizePath(candidatePath);
        if (normalizedCandidate == normalizedParentPath)
        {
            return false;
        }

        if (!normalizedCandidate.StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remain = normalizedCandidate[parentPrefix.Length..];
        if (string.IsNullOrWhiteSpace(remain))
        {
            return false;
        }

        return !remain.Contains('/');
    }

    public static string NormalizePath(string path)
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

    public static LtfsDirectory? FindDirectoryByPath(LtfsDirectory root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return root;
        }

        var trimmed = path.Trim('/');
        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);

        LtfsDirectory? curr = root;
        foreach (var seg in segments)
        {
            if (curr is null)
            {
                return null;
            }

            object? next = curr[seg];
            if (next is LtfsDirectory d)
            {
                curr = d;
                continue;
            }

            return null;
        }

        return curr;
    }
}
