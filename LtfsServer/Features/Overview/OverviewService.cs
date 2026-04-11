using LtfsServer.Features.LocalTapes;
using LtfsServer.Features.TapeDrives;
using LtfsServer.Features.Tasks;

using Microsoft.Extensions.Logging;

namespace LtfsServer.Features.Overview;

public sealed class OverviewService : IOverviewService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(15);
    private static readonly string[] ExecutionStatusOrder =
    [
        TaskExecutionStatus.Pending,
        TaskExecutionStatus.Running,
        TaskExecutionStatus.WaitingForConfirmation,
        TaskExecutionStatus.Completed,
        TaskExecutionStatus.Failed,
        TaskExecutionStatus.Cancelled,
    ];

    private readonly object _cacheLock = new();
    private readonly ITapeDriveService _tapeDriveService;
    private readonly ILocalTapeRegistry _localTapeRegistry;
    private readonly ITaskGroupService _taskGroupService;
    private readonly ITaskExecutionService _taskExecutionService;
    private readonly ILogger<OverviewService> _logger;

    private OverviewSnapshotDto? _cachedSnapshot;
    private DateTime _cachedAtUtc = DateTime.MinValue;

    public OverviewService(
        ITapeDriveService tapeDriveService,
        ILocalTapeRegistry localTapeRegistry,
        ITaskGroupService taskGroupService,
        ITaskExecutionService taskExecutionService,
        ILogger<OverviewService> logger)
    {
        _tapeDriveService = tapeDriveService;
        _localTapeRegistry = localTapeRegistry;
        _taskGroupService = taskGroupService;
        _taskExecutionService = taskExecutionService;
        _logger = logger;
    }

    public OverviewSnapshotDto GetSnapshot()
    {
        lock (_cacheLock)
        {
            if (_cachedSnapshot is not null && DateTime.UtcNow - _cachedAtUtc <= CacheDuration)
            {
                return _cachedSnapshot;
            }

            var snapshot = new OverviewSnapshotDto
            {
                GeneratedAtTicks = DateTime.UtcNow.Ticks,
                Drives = BuildDriveOverview(),
                Tapes = BuildTapeOverview(),
                Tasks = BuildTaskOverview(),
            };

            _cachedSnapshot = snapshot;
            _cachedAtUtc = DateTime.UtcNow;
            return snapshot;
        }
    }

    private TapeDriveOverviewDto BuildDriveOverview()
    {
        var driveInfos = _tapeDriveService.ScanAndSync();
        var stateCounts = Enum.GetValues<TapeDriveState>()
            .ToDictionary(state => ToDriveStateKey(state), _ => 0, StringComparer.OrdinalIgnoreCase);

        var loadedCount = 0;
        var ltfsReadyCount = 0;

        foreach (var driveInfo in driveInfos)
        {
            try
            {
                var snapshot = _tapeDriveService.GetSnapshot(driveInfo.Id);
                var stateKey = ToDriveStateKey(snapshot.State);
                stateCounts[stateKey] = stateCounts.GetValueOrDefault(stateKey) + 1;

                if (!string.IsNullOrWhiteSpace(snapshot.LoadedBarcode))
                {
                    loadedCount += 1;
                }

                if (snapshot.HasLtfsFilesystem == true)
                {
                    ltfsReadyCount += 1;
                }
            }
            catch (Exception ex)
            {
                stateCounts[ToDriveStateKey(TapeDriveState.Unknown)] += 1;
                _logger.LogWarning(ex, "Failed to capture overview snapshot for tape drive {TapeDriveId}", driveInfo.Id);
            }
            finally
            {
                _tapeDriveService.ReleaseCachedLtfsContext(driveInfo.Id);
            }
        }

        return new TapeDriveOverviewDto
        {
            TotalCount = driveInfos.Count,
            FakeCount = driveInfos.Count(info => info.IsFake),
            LoadedCount = loadedCount,
            LtfsReadyCount = ltfsReadyCount,
            StateCounts = Enum.GetValues<TapeDriveState>()
                .Select(state => new OverviewCountDto
                {
                    Key = ToDriveStateKey(state),
                    Count = stateCounts.GetValueOrDefault(ToDriveStateKey(state)),
                })
                .ToList(),
        };
    }

    private TapeInventoryOverviewDto BuildTapeOverview()
    {
        var tapes = _localTapeRegistry.GetTapeSummaries().ToArray();
        long totalCapacityBytes = 0;
        long freeCapacityBytes = 0;

        foreach (var tape in tapes)
        {
            totalCapacityBytes += Math.Max(0, tape.TotalSizeBytes);
            freeCapacityBytes += Math.Max(0, tape.FreeSizeBytes);
        }

        return new TapeInventoryOverviewDto
        {
            TotalCount = tapes.Length,
            TotalCapacityBytes = totalCapacityBytes,
            FreeCapacityBytes = freeCapacityBytes,
            UsedCapacityBytes = Math.Max(0, totalCapacityBytes - freeCapacityBytes),
        };
    }

    private TaskOverviewDto BuildTaskOverview()
    {
        var groups = _taskGroupService.ListGroups();
        var executions = _taskExecutionService.ListExecutions();
        var statusCounts = ExecutionStatusOrder.ToDictionary(status => status, _ => 0, StringComparer.OrdinalIgnoreCase);

        foreach (var execution in executions)
        {
            var status = string.IsNullOrWhiteSpace(execution.Status)
                ? TaskExecutionStatus.Pending
                : execution.Status;

            statusCounts[status] = statusCounts.GetValueOrDefault(status) + 1;
        }

        return new TaskOverviewDto
        {
            GroupCount = groups.Count,
            QueuedTaskCount = groups.Sum(group => group.Tasks.Count),
            TotalExecutionCount = executions.Count,
            ActiveExecutionCount =
                statusCounts.GetValueOrDefault(TaskExecutionStatus.Pending) +
                statusCounts.GetValueOrDefault(TaskExecutionStatus.Running) +
                statusCounts.GetValueOrDefault(TaskExecutionStatus.WaitingForConfirmation),
            ExecutionStatusCounts = ExecutionStatusOrder
                .Select(status => new OverviewCountDto
                {
                    Key = status,
                    Count = statusCounts.GetValueOrDefault(status),
                })
                .ToList(),
        };
    }

    private static string ToDriveStateKey(TapeDriveState state) => state.ToString().ToLowerInvariant();
}
