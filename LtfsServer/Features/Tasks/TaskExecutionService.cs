using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Ltfs;

using LtfsServer.Features.TapeDrives;

using Microsoft.Extensions.Logging;

using TapeDrive;

namespace LtfsServer.Features.Tasks;

public sealed class TaskExecutionService : ITaskExecutionService
{
    private sealed class ExecutionPreparation
    {
        public required string TapeBarcode { get; init; }
        public required string TapeDriveId { get; init; }
        public required bool NeedsAutoFormat { get; init; }
        public required string? LoadedBarcode { get; init; }
        public required bool? HasLtfsFilesystem { get; init; }
    }

    private sealed class PendingIncidentState
    {
        public required TaskExecutionIncidentDto Incident { get; init; }
        public required TaskCompletionSource<TapeDriveIncidentResolution> Completion { get; init; }
    }

    private sealed class ExecutionState
    {
        public required TaskExecutionSnapshot Snapshot { get; init; }
        public readonly object Sync = new();
        public PendingIncidentState? PendingIncident { get; set; }
    }

    private readonly ITaskGroupService _taskGroupService;
    private readonly ITapeDriveRegistry _tapeDriveRegistry;
    private readonly ITapeDriveService _tapeDriveService;
    private readonly ILogger<TaskExecutionService> _logger;
    private readonly ConcurrentDictionary<string, ExecutionState> _executions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Channel<TaskExecutionEventEnvelope>> _subscribers = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _executionGate = new(1, 1);

    public TaskExecutionService(
        ITaskGroupService taskGroupService,
        ITapeDriveRegistry tapeDriveRegistry,
        ITapeDriveService tapeDriveService,
        ILogger<TaskExecutionService> logger)
    {
        _taskGroupService = taskGroupService;
        _tapeDriveRegistry = tapeDriveRegistry;
        _tapeDriveService = tapeDriveService;
        _logger = logger;
    }

    public IReadOnlyList<TaskExecutionSnapshot> ListExecutions()
    {
        return _executions.Values
            .Select(state => CloneSnapshot(state.Snapshot))
            .OrderByDescending(snapshot => snapshot.UpdatedAtTicks)
            .ToArray();
    }

    public TaskExecutionSnapshot StartExecution(string tapeBarcode, string tapeDriveId)
    {
        tapeBarcode = NormalizeRequired(tapeBarcode, nameof(tapeBarcode));
        tapeDriveId = NormalizeRequired(tapeDriveId, nameof(tapeDriveId));

        if (ListExecutions().Any(snapshot => snapshot.Status is TaskExecutionStatus.Pending or TaskExecutionStatus.Running or TaskExecutionStatus.WaitingForConfirmation))
            throw new InvalidOperationException("Another task execution is already running.");

        if (!_tapeDriveRegistry.TryGet(tapeDriveId, out var drive) || drive is null)
            throw new KeyNotFoundException($"Tape drive '{tapeDriveId}' was not found.");

        var group = _taskGroupService.ListGroups()
            .FirstOrDefault(g => string.Equals(g.TapeBarcode, tapeBarcode, StringComparison.OrdinalIgnoreCase));
        if (group is null || group.Tasks.Count == 0)
            throw new InvalidOperationException($"Task group '{tapeBarcode}' is empty.");

        var preparation = PrepareExecution(group, tapeDriveId);

        var snapshot = new TaskExecutionSnapshot
        {
            ExecutionId = Guid.NewGuid().ToString("N"),
            TapeBarcode = preparation.TapeBarcode,
            TapeDriveId = tapeDriveId,
            Status = TaskExecutionStatus.Pending,
            StartedAtTicks = DateTime.UtcNow.Ticks,
            UpdatedAtTicks = DateTime.UtcNow.Ticks,
        };

        var state = new ExecutionState { Snapshot = snapshot };
        _executions[snapshot.ExecutionId] = state;
        Publish(new TaskExecutionEventEnvelope { Type = "execution-updated", Execution = CloneSnapshot(snapshot) });

        _ = Task.Run(() => ExecuteAsync(state, group, drive, preparation));
        return CloneSnapshot(snapshot);
    }

    public TaskExecutionSnapshot GetExecution(string executionId)
    {
        var state = GetState(executionId);
        return CloneSnapshot(state.Snapshot);
    }

    public TaskExecutionIncidentDto ResolveIncident(string executionId, string incidentId, string resolution)
    {
        var state = GetState(executionId);
        resolution = NormalizeRequired(resolution, nameof(resolution)).ToLowerInvariant();
        var mapped = resolution switch
        {
            "continue" => TapeDriveIncidentResolution.Continue,
            "abort" => TapeDriveIncidentResolution.Abort,
            _ => throw new ArgumentException("Resolution must be 'continue' or 'abort'.")
        };

        PendingIncidentState pending;
        lock (state.Sync)
        {
            pending = state.PendingIncident
                ?? throw new KeyNotFoundException("No pending incident was found for this execution.");

            if (!string.Equals(pending.Incident.IncidentId, incidentId, StringComparison.OrdinalIgnoreCase))
                throw new KeyNotFoundException($"Incident '{incidentId}' was not found.");

            pending.Incident.IsResolved = true;
            pending.Incident.Resolution = resolution;
            pending.Incident.ResolvedAtTicks = DateTime.UtcNow.Ticks;
            state.PendingIncident = null;

            state.Snapshot.PendingIncident = null;
            state.Snapshot.Status = mapped == TapeDriveIncidentResolution.Continue
                ? TaskExecutionStatus.Running
                : TaskExecutionStatus.Cancelled;
            state.Snapshot.UpdatedAtTicks = DateTime.UtcNow.Ticks;
        }

        pending.Completion.TrySetResult(mapped);

        PublishLog(state, mapped == TapeDriveIncidentResolution.Continue ? "info" : "warning",
            mapped == TapeDriveIncidentResolution.Continue
                ? $"Incident '{incidentId}' resolved with continue."
                : $"Incident '{incidentId}' resolved with abort.");

        Publish(new TaskExecutionEventEnvelope
        {
            Type = "incident-resolved",
            Incident = CloneIncident(pending.Incident),
            Execution = CloneSnapshot(state.Snapshot),
        });

        return CloneIncident(pending.Incident);
    }

    public async IAsyncEnumerable<TaskExecutionEventEnvelope> SubscribeAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var subscriberId = Guid.NewGuid().ToString("N");
        var channel = Channel.CreateUnbounded<TaskExecutionEventEnvelope>();
        _subscribers[subscriberId] = channel;

        foreach (var snapshot in ListExecutions())
        {
            await channel.Writer.WriteAsync(new TaskExecutionEventEnvelope
            {
                Type = "execution-updated",
                Execution = snapshot,
            }, cancellationToken);
        }

        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
                yield return item;
        }
        finally
        {
            _subscribers.TryRemove(subscriberId, out _);
            channel.Writer.TryComplete();
        }
    }

    private async Task ExecuteAsync(ExecutionState state, TapeFsTaskGroup group, TapeDriveBase drive, ExecutionPreparation preparation)
    {
        await _executionGate.WaitAsync();
        try
        {
            UpdateStatus(state, TaskExecutionStatus.Running);
            PublishLog(state, "info", $"Started task group '{group.TapeBarcode}' on drive '{preparation.TapeDriveId}'.");
            if (!string.IsNullOrWhiteSpace(preparation.LoadedBarcode))
            {
                PublishLog(state, "info", $"Mounted tape barcode: {preparation.LoadedBarcode}.");
            }

            var ltfs = preparation.HasLtfsFilesystem == true
                ? _tapeDriveService.GetCachedLtfsContext(preparation.TapeDriveId) ?? new Ltfs.Ltfs()
                : new Ltfs.Ltfs();

            ltfs.SetTapeDrive(drive);
            ltfs.ProgressUpdated += (_, snapshot) =>
            {
                lock (state.Sync)
                {
                    state.Snapshot.Progress = new TaskExecutionProgressDto
                    {
                        QueueType = snapshot.QueueType,
                        TotalItems = snapshot.TotalItems,
                        CompletedItems = snapshot.CompletedItems,
                        TotalBytes = snapshot.TotalBytes,
                        ProcessedBytes = snapshot.ProcessedBytes,
                        CurrentItemPath = snapshot.CurrentItemPath,
                        CurrentItemBytes = snapshot.CurrentItemBytes,
                        CurrentItemTotalBytes = snapshot.CurrentItemTotalBytes,
                        InstantBytesPerSecond = snapshot.InstantBytesPerSecond,
                        AverageBytesPerSecond = snapshot.AverageBytesPerSecond,
                        EstimatedRemainingSeconds = snapshot.EstimatedRemainingSeconds,
                        StatusMessage = snapshot.StatusMessage,
                        IsCompleted = snapshot.IsCompleted,
                        TimestampUtcTicks = snapshot.TimestampUtc.Ticks,
                    };
                    state.Snapshot.UpdatedAtTicks = DateTime.UtcNow.Ticks;
                }

                Publish(new TaskExecutionEventEnvelope
                {
                    Type = "execution-updated",
                    Execution = CloneSnapshot(state.Snapshot),
                });
            };
            ltfs.TapeDriveIncidentHandler = incident => HandleIncident(state, incident);

            var formatTask = group.Tasks.FirstOrDefault(task => string.Equals(task.Type, TapeFsTaskType.Format, StringComparison.OrdinalIgnoreCase));
            if (preparation.NeedsAutoFormat)
            {
                var effectiveFormatTask = formatTask?.FormatTask ?? new Ltfs.Tasks.FormatTask
                {
                    FormatParam = new FormatParam
                    {
                        Barcode = group.TapeBarcode,
                        VolumeName = group.Name,
                    }
                };

                ltfs.ExtraPartitionCount = effectiveFormatTask.FormatParam.ExtraPartitionCount;
                PublishLog(state, "warning", "No LTFS filesystem detected. Formatting tape before applying queued tasks.");
                ltfs.Format(effectiveFormatTask.FormatParam);
            }
            else if (formatTask?.FormatTask is not null)
            {
                ltfs.ExtraPartitionCount = formatTask.FormatTask.FormatParam.ExtraPartitionCount;
                PublishLog(state, "info", "Running explicit format task before applying queued tasks.");
                ltfs.Format(formatTask.FormatTask.FormatParam);
            }
            else
            {
                PublishLog(state, "info", "Loaded existing LTFS filesystem from mounted tape.");
                if (!ltfs.ReadLtfs())
                {
                    var fallbackFormat = new Ltfs.Tasks.FormatTask
                    {
                        FormatParam = new FormatParam
                        {
                            Barcode = group.TapeBarcode,
                            VolumeName = group.Name,
                        }
                    };

                    ltfs.ExtraPartitionCount = fallbackFormat.FormatParam.ExtraPartitionCount;
                    PublishLog(state, "warning", "LTFS read failed during execution. Falling back to formatting before queued tasks.");
                    ltfs.Format(fallbackFormat.FormatParam);
                }
            }

            foreach (var task in group.Tasks.OrderBy(task => task.CreatedAtTicks))
            {
                if (string.Equals(task.Type, TapeFsTaskType.Format, StringComparison.OrdinalIgnoreCase))
                    continue;

                MapTask(ltfs, task);
            }

            var writeSuccess = ltfs.GetPendingWriteTasks().Count == 0 || await ltfs.PerformWriteTasks();
            var readSuccess = writeSuccess && (ltfs.GetPendingReadTasks().Count == 0 || await ltfs.PerformReadTasks());
            var verifySuccess = readSuccess && (ltfs.GetPendingVerifyTasks().Count == 0 || await ltfs.PerformVerifyTasks());

            if (!writeSuccess || !readSuccess || !verifySuccess)
            {
                PublishLog(state, "error", state.Snapshot.Error ?? "Task execution did not complete successfully.");
                UpdateStatus(state, state.Snapshot.Status == TaskExecutionStatus.Cancelled ? TaskExecutionStatus.Cancelled : TaskExecutionStatus.Failed);
                return;
            }

            PublishLog(state, "info", "Task execution completed.");
            _tapeDriveService.UpdateCachedLtfsContext(preparation.TapeDriveId, ltfs, group.TapeBarcode);
            UpdateStatus(state, TaskExecutionStatus.Completed);
        }
        catch (TapeDriveCommandException ex)
        {
            lock (state.Sync)
            {
                state.Snapshot.Error = ex.Message;
            }
            PublishLog(state, "error", ex.Message);
            UpdateStatus(state, ex.Incident.Action == TapeDriveIncidentAction.StopAllOperations ? TaskExecutionStatus.Cancelled : TaskExecutionStatus.Failed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task execution {ExecutionId} failed", state.Snapshot.ExecutionId);
            lock (state.Sync)
            {
                state.Snapshot.Error = ex.Message;
            }
            PublishLog(state, "error", ex.Message);
            UpdateStatus(state, TaskExecutionStatus.Failed);
        }
        finally
        {
            _executionGate.Release();
        }
    }

    private ExecutionPreparation PrepareExecution(TapeFsTaskGroup group, string tapeDriveId)
    {
        var snapshot = _tapeDriveService.GetSnapshot(tapeDriveId);

        if (snapshot.State == TapeDriveState.Empty)
        {
            throw new InvalidOperationException($"Tape drive '{tapeDriveId}' does not have a loaded tape.");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.LoadedBarcode)
            && !string.Equals(snapshot.LoadedBarcode, group.TapeBarcode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Mounted tape '{snapshot.LoadedBarcode}' does not match task group '{group.TapeBarcode}'.");
        }

        var hasExplicitFormatTask = group.Tasks.Any(task => string.Equals(task.Type, TapeFsTaskType.Format, StringComparison.OrdinalIgnoreCase));
        var needsAutoFormat = snapshot.HasLtfsFilesystem == false && !hasExplicitFormatTask;

        return new ExecutionPreparation
        {
            TapeBarcode = group.TapeBarcode,
            TapeDriveId = tapeDriveId,
            NeedsAutoFormat = needsAutoFormat,
            LoadedBarcode = snapshot.LoadedBarcode,
            HasLtfsFilesystem = snapshot.HasLtfsFilesystem,
        };
    }

    private TapeDriveIncidentResolution HandleIncident(ExecutionState state, TapeDriveIncident incident)
    {
        var dto = new TaskExecutionIncidentDto
        {
            IncidentId = Guid.NewGuid().ToString("N"),
            ExecutionId = state.Snapshot.ExecutionId,
            Source = incident.Source.ToString(),
            Severity = incident.Severity.ToString(),
            Action = incident.Action.ToString(),
            Message = incident.Message,
            Detail = incident.Detail,
            RequiresConfirmation = incident.Action == TapeDriveIncidentAction.PauseCurrentTasks,
            CreatedAtTicks = DateTime.UtcNow.Ticks,
        };

        if (incident.Action == TapeDriveIncidentAction.NotifyOnly)
        {
            PublishLog(state, "warning", dto.Message);
            Publish(new TaskExecutionEventEnvelope { Type = "incident-raised", Incident = dto, Execution = CloneSnapshot(state.Snapshot) });
            return TapeDriveIncidentResolution.Continue;
        }

        if (incident.Action == TapeDriveIncidentAction.StopAllOperations)
        {
            lock (state.Sync)
            {
                state.Snapshot.Error = dto.Message;
                state.Snapshot.UpdatedAtTicks = DateTime.UtcNow.Ticks;
            }
            PublishLog(state, "error", dto.Message);
            Publish(new TaskExecutionEventEnvelope { Type = "incident-raised", Incident = dto, Execution = CloneSnapshot(state.Snapshot) });
            return TapeDriveIncidentResolution.Abort;
        }

        var pending = new PendingIncidentState
        {
            Incident = dto,
            Completion = new TaskCompletionSource<TapeDriveIncidentResolution>(TaskCreationOptions.RunContinuationsAsynchronously),
        };

        lock (state.Sync)
        {
            state.PendingIncident = pending;
            state.Snapshot.PendingIncident = dto;
            state.Snapshot.Status = TaskExecutionStatus.WaitingForConfirmation;
            state.Snapshot.UpdatedAtTicks = DateTime.UtcNow.Ticks;
        }

        PublishLog(state, "warning", dto.Message);
        Publish(new TaskExecutionEventEnvelope { Type = "incident-raised", Incident = CloneIncident(dto), Execution = CloneSnapshot(state.Snapshot) });
        return pending.Completion.Task.GetAwaiter().GetResult();
    }

    private static void MapTask(Ltfs.Ltfs ltfs, TapeFsTask task)
    {
        if (task.PathTask is not null)
        {
            var pathTask = task.PathTask;
            switch (pathTask.Operation.ToLowerInvariant())
            {
                case TapeFsTaskType.Add:
                    if (pathTask.IsDirectory)
                        ltfs.CreateDirectory(pathTask.Path);
                    else
                        ltfs.AddFile(pathTask.LocalPath, pathTask.Path);
                    return;
                case TapeFsTaskType.Update:
                    ltfs.AddFile(pathTask.LocalPath, pathTask.Path);
                    return;
                case TapeFsTaskType.Rename:
                    ltfs.MovePath(pathTask.Path, pathTask.NewPath ?? throw new InvalidOperationException("Rename task requires NewPath."));
                    return;
                case TapeFsTaskType.Delete:
                    ltfs.DeletePath(pathTask.Path);
                    return;
            }
        }

        if (task.ReadTask is not null)
        {
            ltfs.AddReadTask(task.ReadTask.SourcePath, task.ReadTask.TargetPath);
            return;
        }

        throw new InvalidOperationException($"Unsupported task '{task.Id}'.");
    }

    private void UpdateStatus(ExecutionState state, string status)
    {
        lock (state.Sync)
        {
            state.Snapshot.Status = status;
            state.Snapshot.UpdatedAtTicks = DateTime.UtcNow.Ticks;
            if (status is TaskExecutionStatus.Completed or TaskExecutionStatus.Failed or TaskExecutionStatus.Cancelled)
                state.Snapshot.CompletedAtTicks = DateTime.UtcNow.Ticks;
        }

        Publish(new TaskExecutionEventEnvelope { Type = "execution-updated", Execution = CloneSnapshot(state.Snapshot) });
    }

    private void PublishLog(ExecutionState state, string level, string message)
    {
        var log = new TaskExecutionLogEntryDto
        {
            LogId = Guid.NewGuid().ToString("N"),
            ExecutionId = state.Snapshot.ExecutionId,
            TapeDriveId = state.Snapshot.TapeDriveId,
            Level = level,
            Message = message,
            CreatedAtTicks = DateTime.UtcNow.Ticks,
        };

        Publish(new TaskExecutionEventEnvelope
        {
            Type = "log-added",
            Execution = CloneSnapshot(state.Snapshot),
            Log = log,
        });
    }

    private void Publish(TaskExecutionEventEnvelope envelope)
    {
        foreach (var subscriber in _subscribers.Values)
            subscriber.Writer.TryWrite(envelope);
    }

    private ExecutionState GetState(string executionId)
    {
        executionId = NormalizeRequired(executionId, nameof(executionId));
        if (!_executions.TryGetValue(executionId, out var state))
            throw new KeyNotFoundException($"Execution '{executionId}' was not found.");

        return state;
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException($"{paramName} is required.");

        return normalized;
    }

    private static TaskExecutionSnapshot CloneSnapshot(TaskExecutionSnapshot snapshot)
    {
        return new TaskExecutionSnapshot
        {
            ExecutionId = snapshot.ExecutionId,
            TapeBarcode = snapshot.TapeBarcode,
            TapeDriveId = snapshot.TapeDriveId,
            Status = snapshot.Status,
            Error = snapshot.Error,
            StartedAtTicks = snapshot.StartedAtTicks,
            UpdatedAtTicks = snapshot.UpdatedAtTicks,
            CompletedAtTicks = snapshot.CompletedAtTicks,
            Progress = snapshot.Progress is null ? null : new TaskExecutionProgressDto
            {
                QueueType = snapshot.Progress.QueueType,
                TotalItems = snapshot.Progress.TotalItems,
                CompletedItems = snapshot.Progress.CompletedItems,
                TotalBytes = snapshot.Progress.TotalBytes,
                ProcessedBytes = snapshot.Progress.ProcessedBytes,
                CurrentItemPath = snapshot.Progress.CurrentItemPath,
                CurrentItemBytes = snapshot.Progress.CurrentItemBytes,
                CurrentItemTotalBytes = snapshot.Progress.CurrentItemTotalBytes,
                InstantBytesPerSecond = snapshot.Progress.InstantBytesPerSecond,
                AverageBytesPerSecond = snapshot.Progress.AverageBytesPerSecond,
                EstimatedRemainingSeconds = snapshot.Progress.EstimatedRemainingSeconds,
                StatusMessage = snapshot.Progress.StatusMessage,
                IsCompleted = snapshot.Progress.IsCompleted,
                TimestampUtcTicks = snapshot.Progress.TimestampUtcTicks,
            },
            PendingIncident = snapshot.PendingIncident is null ? null : CloneIncident(snapshot.PendingIncident),
        };
    }

    private static TaskExecutionIncidentDto CloneIncident(TaskExecutionIncidentDto incident)
    {
        return new TaskExecutionIncidentDto
        {
            IncidentId = incident.IncidentId,
            ExecutionId = incident.ExecutionId,
            Source = incident.Source,
            Severity = incident.Severity,
            Action = incident.Action,
            Message = incident.Message,
            Detail = incident.Detail,
            RequiresConfirmation = incident.RequiresConfirmation,
            IsResolved = incident.IsResolved,
            Resolution = incident.Resolution,
            CreatedAtTicks = incident.CreatedAtTicks,
            ResolvedAtTicks = incident.ResolvedAtTicks,
        };
    }
}