namespace LtfsServer.Features.Tasks;

public sealed class TaskExecutionTapePerformanceDto
{
    public int RepositionsPer100MB { get; set; }
    public double DataRateIntoBufferMBPerSecond { get; set; }
    public double MaximumDataRateMBPerSecond { get; set; }
    public double CurrentDataRateMBPerSecond { get; set; }
    public double NativeDataRateMBPerSecond { get; set; }
    public double CompressionRatio { get; set; }
}

public sealed class TaskExecutionChannelErrorRateDto
{
    public int ChannelNumber { get; set; }
    public double? ErrorRateLog10 { get; set; }
    public bool IsNegativeInfinity { get; set; }
    public double HeatLevel { get; set; }
    public string DisplayValue { get; set; } = string.Empty;
}

public sealed class TaskExecutionSpeedSampleDto
{
    public long TimestampUtcTicks { get; set; }
    public double SpeedMBPerSecond { get; set; }
}

public sealed class TaskExecutionChannelErrorHistorySampleDto
{
    public long TimestampUtcTicks { get; set; }
    public TaskExecutionChannelErrorRateDto[] ChannelErrorRates { get; set; } = [];
}

public static class TaskExecutionStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string WaitingForConfirmation = "waiting-for-confirmation";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

public sealed class TaskExecutionProgressDto
{
    public string QueueType { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public ulong TotalBytes { get; set; }
    public ulong ProcessedBytes { get; set; }
    public ulong RemainingBytes { get; set; }
    public string? CurrentItemPath { get; set; }
    public string? CurrentItemName { get; set; }
    public long CurrentItemBytes { get; set; }
    public long CurrentItemTotalBytes { get; set; }
    public double CurrentItemPercentComplete { get; set; }
    public double InstantBytesPerSecond { get; set; }
    public double AverageBytesPerSecond { get; set; }
    public double InstantSpeedMBPerSecond { get; set; }
    public double AverageSpeedMBPerSecond { get; set; }
    public double EstimatedRemainingSeconds { get; set; }
    public double PercentComplete { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public long TimestampUtcTicks { get; set; }
    public TaskExecutionTapePerformanceDto? TapePerformance { get; set; }
    public TaskExecutionChannelErrorRateDto[]? ChannelErrorRates { get; set; }
    public TaskExecutionChannelErrorRateDto? HighestChannelErrorRate { get; set; }
    public TaskExecutionSpeedSampleDto[] SpeedHistory { get; set; } = [];
    public TaskExecutionChannelErrorHistorySampleDto[] ChannelErrorRateHistory { get; set; } = [];
}

public sealed class TaskExecutionIncidentDto
{
    public string IncidentId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public bool RequiresConfirmation { get; set; }
    public bool IsResolved { get; set; }
    public string? Resolution { get; set; }
    public long CreatedAtTicks { get; set; }
    public long? ResolvedAtTicks { get; set; }
}

public sealed class TaskExecutionLogEntryDto
{
    public string LogId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public string TapeDriveId { get; set; } = string.Empty;
    public string Level { get; set; } = "info";
    public string Message { get; set; } = string.Empty;
    public long CreatedAtTicks { get; set; }
}

public sealed class TaskExecutionSnapshot
{
    public string ExecutionId { get; set; } = string.Empty;
    public string TapeBarcode { get; set; } = string.Empty;
    public string TapeDriveId { get; set; } = string.Empty;
    public string Status { get; set; } = TaskExecutionStatus.Pending;
    public string? Error { get; set; }
    public long StartedAtTicks { get; set; }
    public long UpdatedAtTicks { get; set; }
    public long? CompletedAtTicks { get; set; }
    public bool ScsiMetricsEnabled { get; set; } = true;
    public TaskExecutionProgressDto? Progress { get; set; }
    public TaskExecutionIncidentDto? PendingIncident { get; set; }
}

public sealed class TaskExecutionEventEnvelope
{
    public string Type { get; set; } = string.Empty;
    public TaskExecutionSnapshot? Execution { get; set; }
    public TaskExecutionIncidentDto? Incident { get; set; }
    public TaskExecutionLogEntryDto? Log { get; set; }
}

public sealed class ExecuteTapeFsTaskGroupRequest
{
    public string TapeDriveId { get; set; } = string.Empty;
    public bool ScsiMetricsEnabled { get; set; } = true;
}

public sealed class ResolveTaskExecutionIncidentRequest
{
    public string Resolution { get; set; } = string.Empty;
}

public sealed class UpdateTaskExecutionMetricsRequest
{
    public bool Enabled { get; set; }
}