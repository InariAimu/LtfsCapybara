namespace Ltfs.Tasks;

public enum TaskExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Committed,
}

public abstract class TaskBase
{
    public long SequenceNumber { get; internal set; }

    public TaskExecutionStatus Status { get; set; } = TaskExecutionStatus.Pending;

    public bool IsTaskDone => Status is TaskExecutionStatus.Completed or TaskExecutionStatus.Cancelled or TaskExecutionStatus.Committed;
    public bool IsTaskError => Status is TaskExecutionStatus.Failed or TaskExecutionStatus.Cancelled;

    public DateTime StartTime { get; set; } = DateTime.MinValue;
    public DateTime EndTime { get; set; } = DateTime.MinValue;
}
