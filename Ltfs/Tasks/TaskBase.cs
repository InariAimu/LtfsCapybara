namespace Ltfs.Tasks;

public enum TaskExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Committed,
}

public abstract class TaskBase
{
    public long SequenceNumber { get; internal set; }

    public TaskExecutionStatus Status { get; set; } = TaskExecutionStatus.Pending;

    public bool IsTaskDone => Status is TaskExecutionStatus.Completed or TaskExecutionStatus.Committed;
    public bool IsTaskError => Status == TaskExecutionStatus.Failed;

    public DateTime StartTime { get; set; } = DateTime.MinValue;
    public DateTime EndTime { get; set; } = DateTime.MinValue;
}
