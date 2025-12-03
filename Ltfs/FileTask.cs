using Ltfs.Index;

namespace Ltfs;

public enum FileTaskType
{
    Write,
    Replace,
    Delete
}

public class FileTask
{
    public required FileTaskType TaskType { get; set; }

    public required string LocalPath { get; set; }
    public required string TargetPath { get; set; }

    public required LtfsFile LtfsPath { get; set; }

    public bool isTaskDone { get; set; } = false;
}
