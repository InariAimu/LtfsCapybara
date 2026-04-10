namespace LtfsServer.Features.Overview;

public sealed class OverviewCountDto
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class TapeDriveOverviewDto
{
    public int TotalCount { get; set; }
    public int FakeCount { get; set; }
    public int LoadedCount { get; set; }
    public int LtfsReadyCount { get; set; }
    public List<OverviewCountDto> StateCounts { get; set; } = [];
}

public sealed class TapeInventoryOverviewDto
{
    public int TotalCount { get; set; }
    public long TotalCapacityBytes { get; set; }
    public long FreeCapacityBytes { get; set; }
    public long UsedCapacityBytes { get; set; }
}

public sealed class TaskOverviewDto
{
    public int GroupCount { get; set; }
    public int QueuedTaskCount { get; set; }
    public int TotalExecutionCount { get; set; }
    public int ActiveExecutionCount { get; set; }
    public List<OverviewCountDto> ExecutionStatusCounts { get; set; } = [];
}

public sealed class OverviewSnapshotDto
{
    public long GeneratedAtTicks { get; set; }
    public TapeDriveOverviewDto Drives { get; set; } = new();
    public TapeInventoryOverviewDto Tapes { get; set; } = new();
    public TaskOverviewDto Tasks { get; set; } = new();
}
