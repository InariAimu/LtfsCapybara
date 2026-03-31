using LtfsServer.Services;

namespace LtfsServer.API;

internal static class LocalIndexOverlay
{
    public static TaskOverlayState BuildTaskOverlayState(TapeFsTaskGroup? taskGroup)
    {
        var folderActions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fileActions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hasFormatTask = false;

        if (taskGroup?.Tasks is null)
        {
            return new TaskOverlayState(folderActions, fileActions, false, false);
        }

        foreach (var task in taskGroup.Tasks.OrderBy(t => t.CreatedAtTicks))
        {
            if (string.Equals(task.Type, TapeFsTaskType.Format, StringComparison.OrdinalIgnoreCase))
            {
                hasFormatTask = true;
            }

            if (task.PathTask is null)
            {
                continue;
            }

            var operation = (task.PathTask.Operation ?? string.Empty).Trim().ToLowerInvariant();
            if (task.PathTask.IsDirectory)
            {
                var folderPath = LocalIndexPath.NormalizePath(task.PathTask.Path);
                if (operation is "add" or "rename" or "update" or "delete")
                {
                    folderActions[folderPath] = operation;
                }
                continue;
            }

            string? action = operation switch
            {
                TapeFsTaskType.Add => "add",
                TapeFsTaskType.Rename => "rename",
                TapeFsTaskType.Update => "replace",
                TapeFsTaskType.Delete => "delete",
                _ => null,
            };

            if (action is null)
            {
                continue;
            }

            var targetPath = LocalIndexPath.NormalizePath(task.PathTask.Path);
            if (targetPath == "/")
            {
                continue;
            }

            fileActions[targetPath] = action;
        }

        return new TaskOverlayState(folderActions, fileActions, taskGroup.Tasks.Count > 0, hasFormatTask);
    }
}

internal sealed class TaskOverlayState(
    IReadOnlyDictionary<string, string> folderActions,
    IReadOnlyDictionary<string, string> fileActions,
    bool hasTasks,
    bool hasFormatTask)
{
    public IReadOnlyDictionary<string, string> FolderActions { get; } = folderActions;
    public IReadOnlyDictionary<string, string> FileActions { get; } = fileActions;
    public bool HasTasks { get; } = hasTasks;
    public bool HasFormatTask { get; } = hasFormatTask;
}

