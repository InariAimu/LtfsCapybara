using Ltfs.Index;

namespace Ltfs;

public enum FileTaskType
{
    Write,
    Replace,
    Delete
}

public class WriteTask
{
    public required FileTaskType TaskType { get; set; }

    public required string LocalPath { get; set; }
    public required string TargetPath { get; set; }

    public required LtfsFile LtfsPath { get; set; }

    public bool IsTaskDone { get; set; } = false;
    public bool IsTaskError { get; set; } = false;
}
