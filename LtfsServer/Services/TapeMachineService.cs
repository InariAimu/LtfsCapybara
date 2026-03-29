using System;
using System.Collections.Concurrent;
using System.IO;
using LtoTape;
using Microsoft.Extensions.Logging;
using TapeDrive;

namespace LtfsServer.Services;

public class TapeMachineService : ITapeMachineService
{
    private sealed class MachineContext
    {
        public required string TapeDriveId { get; init; }
        public required string DevicePath { get; init; }
        public required bool IsFake { get; init; }
        public TapeMachineState State { get; set; } = TapeMachineState.Empty;
        public string? LastError { get; set; }
    }

    private readonly ITapeDriveRegistry _registry;
    private readonly ITapeDriveService _tapeDriveService;
    private readonly ILocalTapeRegistry _localTapeRegistry;
    private readonly AppData _appData;
    private readonly ILogger<TapeMachineService> _logger;
    private readonly ConcurrentDictionary<string, MachineContext> _contexts = new();
    private readonly ConcurrentDictionary<string, object> _locks = new();

    public TapeMachineService(
        ITapeDriveRegistry registry,
        ITapeDriveService tapeDriveService,
        ILocalTapeRegistry localTapeRegistry,
        AppData appData,
        ILogger<TapeMachineService> logger)
    {
        _registry = registry;
        _tapeDriveService = tapeDriveService;
        _localTapeRegistry = localTapeRegistry;
        _appData = appData;
        _logger = logger;
    }

    public TapeMachineSnapshot GetSnapshot(string tapeDriveId)
    {
        var driveInfo = RefreshAndResolveDrive(tapeDriveId);
        var context = _contexts.AddOrUpdate(
            tapeDriveId,
            _ => new MachineContext
            {
                TapeDriveId = driveInfo.Id,
                DevicePath = driveInfo.DevicePath,
                IsFake = driveInfo.IsFake,
                State = TapeMachineState.Empty,
            },
            (_, old) =>
            {
                old.State = old.State == TapeMachineState.Unknown ? TapeMachineState.Empty : old.State;
                return old;
            });

        return ToSnapshot(context, null);
    }

    public TapeMachineSnapshot Execute(string tapeDriveId, TapeMachineAction action)
    {
        var lockObj = _locks.GetOrAdd(tapeDriveId, _ => new object());

        lock (lockObj)
        {
            var driveInfo = RefreshAndResolveDrive(tapeDriveId);
            if (!_registry.TryGet(tapeDriveId, out var drive) || drive is null)
            {
                throw new InvalidOperationException($"Tape drive '{tapeDriveId}' is unavailable.");
            }

            var context = _contexts.AddOrUpdate(
                tapeDriveId,
                _ => new MachineContext
                {
                    TapeDriveId = driveInfo.Id,
                    DevicePath = driveInfo.DevicePath,
                    IsFake = driveInfo.IsFake,
                    State = TapeMachineState.Empty,
                },
                (_, old) => old);

            EnsureActionAllowed(context.State, action, tapeDriveId);

            CartridgeMemory? cm = null;
            try
            {
                switch (action)
                {
                    case TapeMachineAction.ThreadTape:
                        drive.Load();
                        context.State = TapeMachineState.Threaded;
                        context.LastError = null;
                        break;
                    case TapeMachineAction.LoadTape:
                        drive.LoadUnthread();
                        context.State = TapeMachineState.Loaded;
                        context.LastError = null;
                        break;
                    case TapeMachineAction.UnthreadTape:
                        drive.Unthread();
                        context.State = TapeMachineState.Loaded;
                        context.LastError = null;
                        break;
                    case TapeMachineAction.EjectTape:
                        drive.Unload();
                        context.State = TapeMachineState.Empty;
                        context.LastError = null;
                        break;
                    case TapeMachineAction.ReadInfo:
                        var raw = drive.ReadDiagCM();
                        cm = new CartridgeMemory();
                        cm.FromBytes(raw);
                        SaveCmBinaryAndUpdateRegistry(raw, drive, cm);
                        context.LastError = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown tape action.");
                }
            }
            catch (Exception ex)
            {
                context.State = TapeMachineState.Faulted;
                context.LastError = ex.Message;
                _logger.LogWarning(ex, "Tape action {Action} failed for {TapeDriveId}", action, tapeDriveId);
                throw;
            }

            return ToSnapshot(context, cm);
        }
    }

    private void SaveCmBinaryAndUpdateRegistry(byte[] rawCm, TapeDriveBase drive, CartridgeMemory cm)
    {
        if (rawCm.Length == 0)
        {
            _logger.LogWarning("Skipping CM binary save because ReadDiagCM returned empty data.");
            return;
        }

        var barcode = ResolveBarcode(drive, cm);
        if (string.IsNullOrWhiteSpace(barcode))
        {
            _logger.LogWarning("Skipping CM binary save because barcode is empty.");
            return;
        }

        var timestamp = DateTime.Now;
        var fileName = $"{barcode}_{timestamp:yyyyMMdd}_{timestamp:HHmmss}.{timestamp:FFFFFFF}.cmbin";
        var outputDir = Path.Combine(_appData.Path, "local", barcode);
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, fileName);
        File.WriteAllBytes(outputPath, rawCm);

        _ = _localTapeRegistry.TryUpsertFile(barcode, outputPath);

        _logger.LogInformation("Saved CM binary to {Path}", outputPath);
    }

    private static string ResolveBarcode(TapeDriveBase drive, CartridgeMemory cm)
    {
        var barcode = cm.ApplicationSpecific.BarCode;
        if (string.IsNullOrWhiteSpace(barcode))
        {
            try
            {
                barcode = drive.ReadBarCode();
            }
            catch
            {
                barcode = string.Empty;
            }
        }

        return SanitizeBarcode(barcode);
    }

    private static string SanitizeBarcode(string barcode)
    {
        var safe = (barcode ?? string.Empty).Trim().ToUpperInvariant();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(c, '_');
        }

        return safe;
    }

    private TapeDriveInfo RefreshAndResolveDrive(string tapeDriveId)
    {
        var drives = _tapeDriveService.ScanAndSync();
        var driveInfo = drives.FirstOrDefault(d => string.Equals(d.Id, tapeDriveId, StringComparison.OrdinalIgnoreCase));
        if (driveInfo is null)
        {
            throw new KeyNotFoundException($"Tape drive '{tapeDriveId}' was not found.");
        }

        return driveInfo;
    }

    private static void EnsureActionAllowed(TapeMachineState state, TapeMachineAction action, string tapeDriveId)
    {
        if (GetAllowedActions(state).Contains(action))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Action '{action}' is not allowed when tape drive '{tapeDriveId}' is in state '{state}'.");
    }

    private static IReadOnlyList<TapeMachineAction> GetAllowedActions(TapeMachineState state)
    {
        return state switch
        {
            TapeMachineState.Empty => [TapeMachineAction.ThreadTape, TapeMachineAction.LoadTape],
            TapeMachineState.Loaded => [TapeMachineAction.ThreadTape, TapeMachineAction.EjectTape, TapeMachineAction.ReadInfo],
            TapeMachineState.Threaded => [TapeMachineAction.UnthreadTape, TapeMachineAction.EjectTape, TapeMachineAction.ReadInfo],
            TapeMachineState.Faulted => [TapeMachineAction.EjectTape],
            _ => [TapeMachineAction.ThreadTape, TapeMachineAction.LoadTape],
        };
    }

    private static TapeMachineSnapshot ToSnapshot(MachineContext context, CartridgeMemory? cm)
    {
        return new TapeMachineSnapshot(
            context.TapeDriveId,
            context.DevicePath,
            context.State,
            GetAllowedActions(context.State),
            context.LastError,
            context.IsFake,
            cm);
    }
}
