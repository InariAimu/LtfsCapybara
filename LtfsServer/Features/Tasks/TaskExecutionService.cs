using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Ltfs;
using Ltfs.Tasks;

using LtfsServer.BootStrap;
using LtfsServer.Features.LocalTapes;
using LtfsServer.Features.TapeDrives;

using Microsoft.Extensions.Logging;

using TapeDrive;

namespace LtfsServer.Features.Tasks;

public sealed class TaskExecutionService : ITaskExecutionService
{
    private const double BytesPerMegabyte = 1024d * 1024d;
    private static readonly long SpeedHistoryWindowTicks = TimeSpan.FromMinutes(10).Ticks;

    private static double SanitizeFinite(double value, double fallback = -1d)
    {
        return double.IsFinite(value) ? value : fallback;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;

        return value;
    }

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
        public readonly List<TaskExecutionSpeedSampleDto> SpeedHistory = [];
        public readonly List<TaskExecutionChannelErrorHistorySampleDto> ChannelErrorRateHistory = [];
        public PendingIncidentState? PendingIncident { get; set; }
    }

    private readonly ITaskGroupService _taskGroupService;
    private readonly ILocalTapeRegistry _localTapeRegistry;
    private readonly ITapeDriveRegistry _tapeDriveRegistry;
    private readonly ITapeDriveService _tapeDriveService;
    private readonly AppData _appData;
    private readonly ILogger<TaskExecutionService> _logger;
    private readonly ConcurrentDictionary<string, ExecutionState> _executions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Channel<TaskExecutionEventEnvelope>> _subscribers = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _executionGate = new(1, 1);

    public TaskExecutionService(
        ITaskGroupService taskGroupService,
        ILocalTapeRegistry localTapeRegistry,
        ITapeDriveRegistry tapeDriveRegistry,
        ITapeDriveService tapeDriveService,
        AppData appData,
        ILogger<TaskExecutionService> logger)
    {
        _taskGroupService = taskGroupService;
        _localTapeRegistry = localTapeRegistry;
        _tapeDriveRegistry = tapeDriveRegistry;
        _tapeDriveService = tapeDriveService;
        _appData = appData;
        _logger = logger;
    }

    public IReadOnlyList<TaskExecutionSnapshot> ListExecutions()
    {
        return _executions.Values
            .Select(state => CloneSnapshot(state.Snapshot))
            .OrderByDescending(snapshot => snapshot.UpdatedAtTicks)
            .ToArray();
    }

    public TaskExecutionSnapshot StartExecution(string tapeBarcode, string tapeDriveId, bool scsiMetricsEnabled)
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
            ScsiMetricsEnabled = scsiMetricsEnabled,
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

    public TaskExecutionSnapshot UpdateScsiMetrics(string executionId, bool enabled)
    {
        var state = GetState(executionId);

        lock (state.Sync)
        {
            state.Snapshot.ScsiMetricsEnabled = enabled;
            state.Snapshot.UpdatedAtTicks = DateTime.UtcNow.Ticks;

            if (!enabled && state.Snapshot.Progress is not null)
            {
                state.Snapshot.Progress.TapePerformance = null;
                state.Snapshot.Progress.ChannelErrorRates = null;
                state.Snapshot.Progress.HighestChannelErrorRate = null;
                state.Snapshot.Progress.TimestampUtcTicks = DateTime.UtcNow.Ticks;
            }
        }

        Publish(new TaskExecutionEventEnvelope
        {
            Type = "execution-updated",
            Execution = CloneSnapshot(state.Snapshot),
        });

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
            ltfs.LocalIndexRootPath = Path.Combine(_appData.Path, "local");
            ltfs.ShouldSampleScsiMetrics = () => IsScsiMetricsEnabled(state);
            ltfs.ProgressUpdated += (_, snapshot) =>
            {
                lock (state.Sync)
                {
                    var progress = new TaskExecutionProgressDto
                    {
                        QueueType = snapshot.QueueType,
                        TotalItems = snapshot.TotalItems,
                        CompletedItems = snapshot.CompletedItems,
                        TotalBytes = snapshot.TotalBytes,
                        ProcessedBytes = snapshot.ProcessedBytes,
                        RemainingBytes = snapshot.TotalBytes > snapshot.ProcessedBytes
                            ? snapshot.TotalBytes - snapshot.ProcessedBytes
                            : 0ul,
                        CurrentItemPath = snapshot.CurrentItemPath,
                        CurrentItemName = GetCurrentItemName(snapshot.CurrentItemPath),
                        CurrentItemBytes = snapshot.CurrentItemBytes,
                        CurrentItemTotalBytes = snapshot.CurrentItemTotalBytes,
                        CurrentItemPercentComplete = snapshot.CurrentItemTotalBytes > 0
                            ? Clamp(snapshot.CurrentItemBytes * 100d / snapshot.CurrentItemTotalBytes, 0d, 100d)
                            : 0d,
                        InstantBytesPerSecond = SanitizeFinite(snapshot.InstantBytesPerSecond, 0d),
                        AverageBytesPerSecond = SanitizeFinite(snapshot.AverageBytesPerSecond, 0d),
                        InstantSpeedMBPerSecond = SanitizeFinite(snapshot.InstantBytesPerSecond / BytesPerMegabyte, 0d),
                        AverageSpeedMBPerSecond = SanitizeFinite(snapshot.AverageBytesPerSecond / BytesPerMegabyte, 0d),
                        EstimatedRemainingSeconds = SanitizeFinite(snapshot.EstimatedRemainingSeconds),
                        PercentComplete = snapshot.TotalBytes > 0
                            ? Clamp(snapshot.ProcessedBytes * 100d / snapshot.TotalBytes, 0d, 100d)
                            : 0d,
                        StatusMessage = snapshot.StatusMessage,
                        IsCompleted = snapshot.IsCompleted,
                        TimestampUtcTicks = snapshot.TimestampUtc.Ticks,
                        TapePerformance = snapshot.TapePerformance is null ? null : new TaskExecutionTapePerformanceDto
                        {
                            RepositionsPer100MB = snapshot.TapePerformance.RepositionsPer100MB,
                            DataRateIntoBufferMBPerSecond = SanitizeFinite(snapshot.TapePerformance.DataRateIntoBufferMBPerSecond),
                            MaximumDataRateMBPerSecond = SanitizeFinite(snapshot.TapePerformance.MaximumDataRateMBPerSecond),
                            CurrentDataRateMBPerSecond = SanitizeFinite(snapshot.TapePerformance.CurrentDataRateMBPerSecond),
                            NativeDataRateMBPerSecond = SanitizeFinite(snapshot.TapePerformance.NativeDataRateMBPerSecond),
                            CompressionRatio = SanitizeFinite(snapshot.TapePerformance.CompressionRatio),
                        },
                    };

                    UpdateSpeedHistory(state, progress.TimestampUtcTicks, progress.InstantSpeedMBPerSecond);
                    progress.SpeedHistory = state.SpeedHistory
                        .Select(sample => new TaskExecutionSpeedSampleDto
                        {
                            TimestampUtcTicks = sample.TimestampUtcTicks,
                            SpeedMBPerSecond = sample.SpeedMBPerSecond,
                        })
                        .ToArray();
                    progress.ChannelErrorRates = BuildChannelErrorRates(snapshot.ChannelErrorRates);
                    progress.HighestChannelErrorRate = SelectHighestChannelErrorRate(progress.ChannelErrorRates);
                    UpdateChannelErrorRateHistory(state, progress.TimestampUtcTicks, progress.ChannelErrorRates);
                    progress.ChannelErrorRateHistory = state.ChannelErrorRateHistory
                        .Select(sample => new TaskExecutionChannelErrorHistorySampleDto
                        {
                            TimestampUtcTicks = sample.TimestampUtcTicks,
                            ChannelErrorRates = sample.ChannelErrorRates
                                .Select(rate => CloneChannelErrorRate(rate))
                                .ToArray(),
                        })
                        .ToArray();

                    state.Snapshot.Progress = progress;
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
                var effectiveFormatTask = FormatTaskDefaults.NormalizeFormatTask(formatTask?.FormatTask, group.TapeBarcode, group.Name);

                ltfs.ExtraPartitionCount = effectiveFormatTask.FormatParam.ExtraPartitionCount;
                PublishLog(state, "warning", "No LTFS filesystem detected. Formatting tape before applying queued tasks.");
                ltfs.Format(effectiveFormatTask.FormatParam);
            }
            else if (formatTask?.FormatTask is not null)
            {
                var effectiveFormatTask = FormatTaskDefaults.NormalizeFormatTask(formatTask.FormatTask, group.TapeBarcode, group.Name);
                ltfs.ExtraPartitionCount = effectiveFormatTask.FormatParam.ExtraPartitionCount;
                PublishLog(state, "info", "Running explicit format task before applying queued tasks.");
                ltfs.Format(effectiveFormatTask.FormatParam);
            }
            else
            {
                PublishLog(state, "info", "Loaded existing LTFS filesystem from mounted tape.");
                if (!ltfs.ReadLtfs())
                {
                    var fallbackFormat = FormatTaskDefaults.NormalizeFormatTask(null, group.TapeBarcode, group.Name);

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

            PublishReadTaskOutcomeLogs(state, ltfs);

            if (!writeSuccess || !readSuccess || !verifySuccess)
            {
                PublishLog(state, "error", state.Snapshot.Error ?? "Task execution did not complete successfully.");
                UpdateStatus(state, state.Snapshot.Status == TaskExecutionStatus.Cancelled ? TaskExecutionStatus.Cancelled : TaskExecutionStatus.Failed);
                return;
            }

            PublishLog(state, "info", "Task execution completed.");
            _tapeDriveService.UpdateCachedLtfsContext(preparation.TapeDriveId, ltfs, group.TapeBarcode);
            RefreshLocalIndexRegistry(group.TapeBarcode);

            try
            {
                _taskGroupService.CompleteTasks(group.TapeBarcode, group.Tasks.Select(task => task.Id));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear completed tasks for tape '{TapeBarcode}'", group.TapeBarcode);
            }

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

    private void RefreshLocalIndexRegistry(string tapeBarcode)
    {
        var normalizedBarcode = NormalizeRequired(tapeBarcode, nameof(tapeBarcode));
        var indexDirectory = Path.Combine(_appData.Path, "local", normalizedBarcode);
        if (!Directory.Exists(indexDirectory))
            return;

        foreach (var filePath in Directory.EnumerateFiles(indexDirectory, "*.xml", SearchOption.TopDirectoryOnly))
        {
            _ = _localTapeRegistry.TryUpsertFile(normalizedBarcode, filePath);
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
            RequiresConfirmation = false,
            CreatedAtTicks = DateTime.UtcNow.Ticks,
        };

        if (incident.Action == TapeDriveIncidentAction.PauseCurrentTasks)
        {
            dto.RequiresConfirmation = true;
            var pending = new PendingIncidentState
            {
                Incident = dto,
                Completion = new TaskCompletionSource<TapeDriveIncidentResolution>(TaskCreationOptions.RunContinuationsAsynchronously),
            };

            lock (state.Sync)
            {
                state.PendingIncident = pending;
                state.Snapshot.PendingIncident = CloneIncident(dto);
                state.Snapshot.Status = TaskExecutionStatus.WaitingForConfirmation;
                state.Snapshot.UpdatedAtTicks = DateTime.UtcNow.Ticks;
            }

            PublishLog(state, incident.Severity == TapeDriveIncidentSeverity.Critical ? "error" : "warning", dto.Message);
            Publish(new TaskExecutionEventEnvelope
            {
                Type = "incident-raised",
                Incident = CloneIncident(dto),
                Execution = CloneSnapshot(state.Snapshot),
            });

            return pending.Completion.Task.GetAwaiter().GetResult();
        }

        if (incident.Severity != TapeDriveIncidentSeverity.Critical)
        {
            PublishLog(
                state,
                incident.Severity == TapeDriveIncidentSeverity.Warning ? "warning" : "info",
                dto.Message);
            Publish(new TaskExecutionEventEnvelope
            {
                Type = "incident-raised",
                Incident = CloneIncident(dto),
                Execution = CloneSnapshot(state.Snapshot),
            });
            return TapeDriveIncidentResolution.Continue;
        }

        lock (state.Sync)
        {
            state.Snapshot.Error = dto.Message;
            state.Snapshot.UpdatedAtTicks = DateTime.UtcNow.Ticks;
        }

        PublishLog(state, "error", dto.Message);
        Publish(new TaskExecutionEventEnvelope
        {
            Type = "incident-raised",
            Incident = CloneIncident(dto),
            Execution = CloneSnapshot(state.Snapshot),
        });
        return TapeDriveIncidentResolution.Abort;
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
            if (task.ReadTask.IsDirectoryMarker)
                return;

            ltfs.AddReadTask(task.ReadTask.SourcePath, task.ReadTask.TargetPath);
            return;
        }

        if (task.VerifyTask is not null)
        {
            if (task.VerifyTask.IsDirectoryMarker)
                return;

            ltfs.AddVerifyTask(task.VerifyTask.SourcePath);
            return;
        }

        throw new InvalidOperationException($"Unsupported task '{task.Id}'.");
    }

    private void PublishReadTaskOutcomeLogs(ExecutionState state, Ltfs.Ltfs ltfs)
    {
        foreach (var task in ltfs.GetPendingReadTasks().OfType<ReadTask>())
        {
            if (task.IsDirectoryMarker)
                continue;

            if (task.IntegrityCheckFailed)
            {
                var preservedPath = string.IsNullOrWhiteSpace(task.PreservedTargetPath)
                    ? task.TargetPath
                    : task.PreservedTargetPath;
                PublishLog(
                    state,
                    "warning",
                    $"Integrity check failed for '{task.SourcePath}'. The downloaded file was preserved at '{preservedPath}'. Please verify it manually.");
                continue;
            }

            if (task.Status == Ltfs.Tasks.TaskExecutionStatus.Failed && !string.IsNullOrWhiteSpace(task.FailureMessage))
            {
                PublishLog(
                    state,
                    "error",
                    $"Read failed for '{task.SourcePath}' -> '{task.TargetPath}': {task.FailureMessage}");
            }
        }
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

    private static TaskExecutionChannelErrorRateDto[]? BuildChannelErrorRates(double[]? rates)
    {
        if (rates is null || rates.Length == 0)
            return null;

        return rates
            .Take(16)
            .Select((rate, index) => BuildChannelErrorRate(index + 1, rate))
            .ToArray();
    }

    private static TaskExecutionChannelErrorRateDto BuildChannelErrorRate(int channelNumber, double rate)
    {
        var isNegativeInfinity = double.IsNegativeInfinity(rate);
        var finiteRate = double.IsFinite(rate) ? rate : (double?)null;
        var displayValue = isNegativeInfinity
            ? "-Inf"
            : finiteRate?.ToString("F2") ?? "-";
        var heatLevel = isNegativeInfinity
            ? 0d
            : finiteRate.HasValue
                ? Clamp((finiteRate.Value + 8d) / 8d, 0d, 1d)
                : 0d;

        return new TaskExecutionChannelErrorRateDto
        {
            ChannelNumber = channelNumber,
            ErrorRateLog10 = finiteRate,
            IsNegativeInfinity = isNegativeInfinity,
            HeatLevel = heatLevel,
            DisplayValue = displayValue,
        };
    }

    private static TaskExecutionChannelErrorRateDto? SelectHighestChannelErrorRate(TaskExecutionChannelErrorRateDto[]? rates)
    {
        if (rates is null || rates.Length == 0)
            return null;

        TaskExecutionChannelErrorRateDto? highest = null;
        foreach (var rate in rates)
        {
            if (highest is null)
            {
                highest = rate;
                continue;
            }

            var currentValue = rate.ErrorRateLog10 ?? double.NegativeInfinity;
            var highestValue = highest.ErrorRateLog10 ?? double.NegativeInfinity;
            if (currentValue > highestValue)
                highest = rate;
        }

        return highest is null
            ? null
            : new TaskExecutionChannelErrorRateDto
            {
                ChannelNumber = highest.ChannelNumber,
                ErrorRateLog10 = highest.ErrorRateLog10,
                IsNegativeInfinity = highest.IsNegativeInfinity,
                HeatLevel = highest.HeatLevel,
                DisplayValue = highest.DisplayValue,
            };
    }

    private static void UpdateChannelErrorRateHistory(
        ExecutionState state,
        long timestampUtcTicks,
        TaskExecutionChannelErrorRateDto[]? rates)
    {
        if (rates is null || rates.Length == 0)
            return;

        state.ChannelErrorRateHistory.Add(new TaskExecutionChannelErrorHistorySampleDto
        {
            TimestampUtcTicks = timestampUtcTicks,
            ChannelErrorRates = rates.Select(CloneChannelErrorRate).ToArray(),
        });

        var minTicks = timestampUtcTicks - SpeedHistoryWindowTicks;
        state.ChannelErrorRateHistory.RemoveAll(sample => sample.TimestampUtcTicks < minTicks);
    }

    private static TaskExecutionChannelErrorRateDto CloneChannelErrorRate(TaskExecutionChannelErrorRateDto rate)
    {
        return new TaskExecutionChannelErrorRateDto
        {
            ChannelNumber = rate.ChannelNumber,
            ErrorRateLog10 = rate.ErrorRateLog10,
            IsNegativeInfinity = rate.IsNegativeInfinity,
            HeatLevel = rate.HeatLevel,
            DisplayValue = rate.DisplayValue,
        };
    }

    private static string? GetCurrentItemName(string? currentItemPath)
    {
        if (string.IsNullOrWhiteSpace(currentItemPath))
            return null;

        var fileName = Path.GetFileName(currentItemPath);
        return string.IsNullOrWhiteSpace(fileName) ? currentItemPath : fileName;
    }

    private static void UpdateSpeedHistory(ExecutionState state, long timestampUtcTicks, double speedMBPerSecond)
    {
        state.SpeedHistory.Add(new TaskExecutionSpeedSampleDto
        {
            TimestampUtcTicks = timestampUtcTicks,
            SpeedMBPerSecond = SanitizeFinite(speedMBPerSecond, 0d),
        });

        var minTicks = timestampUtcTicks - SpeedHistoryWindowTicks;
        state.SpeedHistory.RemoveAll(sample => sample.TimestampUtcTicks < minTicks);
    }

    private static bool IsScsiMetricsEnabled(ExecutionState state)
    {
        lock (state.Sync)
            return state.Snapshot.ScsiMetricsEnabled;
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
            ScsiMetricsEnabled = snapshot.ScsiMetricsEnabled,
            Progress = snapshot.Progress is null ? null : new TaskExecutionProgressDto
            {
                QueueType = snapshot.Progress.QueueType,
                TotalItems = snapshot.Progress.TotalItems,
                CompletedItems = snapshot.Progress.CompletedItems,
                TotalBytes = snapshot.Progress.TotalBytes,
                ProcessedBytes = snapshot.Progress.ProcessedBytes,
                RemainingBytes = snapshot.Progress.RemainingBytes,
                CurrentItemPath = snapshot.Progress.CurrentItemPath,
                CurrentItemName = snapshot.Progress.CurrentItemName,
                CurrentItemBytes = snapshot.Progress.CurrentItemBytes,
                CurrentItemTotalBytes = snapshot.Progress.CurrentItemTotalBytes,
                CurrentItemPercentComplete = snapshot.Progress.CurrentItemPercentComplete,
                InstantBytesPerSecond = snapshot.Progress.InstantBytesPerSecond,
                AverageBytesPerSecond = snapshot.Progress.AverageBytesPerSecond,
                InstantSpeedMBPerSecond = snapshot.Progress.InstantSpeedMBPerSecond,
                AverageSpeedMBPerSecond = snapshot.Progress.AverageSpeedMBPerSecond,
                EstimatedRemainingSeconds = snapshot.Progress.EstimatedRemainingSeconds,
                PercentComplete = snapshot.Progress.PercentComplete,
                StatusMessage = snapshot.Progress.StatusMessage,
                IsCompleted = snapshot.Progress.IsCompleted,
                TimestampUtcTicks = snapshot.Progress.TimestampUtcTicks,
                TapePerformance = snapshot.Progress.TapePerformance is null ? null : new TaskExecutionTapePerformanceDto
                {
                    RepositionsPer100MB = snapshot.Progress.TapePerformance.RepositionsPer100MB,
                    DataRateIntoBufferMBPerSecond = snapshot.Progress.TapePerformance.DataRateIntoBufferMBPerSecond,
                    MaximumDataRateMBPerSecond = snapshot.Progress.TapePerformance.MaximumDataRateMBPerSecond,
                    CurrentDataRateMBPerSecond = snapshot.Progress.TapePerformance.CurrentDataRateMBPerSecond,
                    NativeDataRateMBPerSecond = snapshot.Progress.TapePerformance.NativeDataRateMBPerSecond,
                    CompressionRatio = snapshot.Progress.TapePerformance.CompressionRatio,
                },
                ChannelErrorRates = snapshot.Progress.ChannelErrorRates?
                    .Select(rate => new TaskExecutionChannelErrorRateDto
                    {
                        ChannelNumber = rate.ChannelNumber,
                        ErrorRateLog10 = rate.ErrorRateLog10,
                        IsNegativeInfinity = rate.IsNegativeInfinity,
                        HeatLevel = rate.HeatLevel,
                        DisplayValue = rate.DisplayValue,
                    })
                    .ToArray(),
                HighestChannelErrorRate = snapshot.Progress.HighestChannelErrorRate is null ? null : new TaskExecutionChannelErrorRateDto
                {
                    ChannelNumber = snapshot.Progress.HighestChannelErrorRate.ChannelNumber,
                    ErrorRateLog10 = snapshot.Progress.HighestChannelErrorRate.ErrorRateLog10,
                    IsNegativeInfinity = snapshot.Progress.HighestChannelErrorRate.IsNegativeInfinity,
                    HeatLevel = snapshot.Progress.HighestChannelErrorRate.HeatLevel,
                    DisplayValue = snapshot.Progress.HighestChannelErrorRate.DisplayValue,
                },
                SpeedHistory = snapshot.Progress.SpeedHistory
                    .Select(sample => new TaskExecutionSpeedSampleDto
                    {
                        TimestampUtcTicks = sample.TimestampUtcTicks,
                        SpeedMBPerSecond = sample.SpeedMBPerSecond,
                    })
                    .ToArray(),
                ChannelErrorRateHistory = snapshot.Progress.ChannelErrorRateHistory
                    .Select(sample => new TaskExecutionChannelErrorHistorySampleDto
                    {
                        TimestampUtcTicks = sample.TimestampUtcTicks,
                        ChannelErrorRates = sample.ChannelErrorRates
                            .Select(CloneChannelErrorRate)
                            .ToArray(),
                    })
                    .ToArray(),
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