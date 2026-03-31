using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using LtfsServer.BootStrap;
using LtfsServer.Features.LocalTapes;

using LtoTape;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using TapeDrive;

namespace LtfsServer.Features.TapeDrives;

public class TapeDriveService : ITapeDriveService
{
    private sealed class MachineContext
    {
        public required string TapeDriveId { get; init; }
        public required string DevicePath { get; init; }
        public required bool IsFake { get; init; }
        public TapeDriveState State { get; set; } = TapeDriveState.Empty;
        public string? LastError { get; set; }
    }

    private const int MaxTapeDeviceCount = 32;

    private readonly ITapeDriveRegistry _registry;
    private readonly ILogger<TapeDriveService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ILocalTapeRegistry _localTapeRegistry;
    private readonly AppData _appData;
    private readonly object _sync = new();
    private readonly List<string> _managedIds = new();
    private readonly ConcurrentDictionary<string, MachineContext> _contexts = new();
    private readonly ConcurrentDictionary<string, object> _locks = new();

    public TapeDriveService(
        ITapeDriveRegistry registry,
        ILogger<TapeDriveService> logger,
        IConfiguration configuration,
        ILocalTapeRegistry localTapeRegistry,
        AppData appData)
    {
        _registry = registry;
        _logger = logger;
        _configuration = configuration;
        _localTapeRegistry = localTapeRegistry;
        _appData = appData;
    }

    public IReadOnlyList<TapeDriveInfo> ScanAndSync()
    {
        lock (_sync)
        {
            RemoveManagedDrives();

            var discovered = DiscoverDrives();
            var results = new List<TapeDriveInfo>();

            foreach (var entry in discovered)
            {
                if (!_registry.TryAdd(entry.Id, entry.Drive))
                {
                    (entry.Drive as IDisposable)?.Dispose();
                    continue;
                }

                _managedIds.Add(entry.Id);
                results.Add(new TapeDriveInfo(entry.Id, entry.DevicePath, entry.DisplayName, entry.IsFake));
            }

            return results;
        }
    }

    private void RemoveManagedDrives()
    {
        foreach (var id in _managedIds)
        {
            _registry.TryRemove(id, out _);
        }

        _managedIds.Clear();
    }

    private List<DiscoveredDrive> DiscoverDrives()
    {
        if (_configuration.GetValue<bool>("TapeDrive:UseFakeDrive"))
        {
            return [
                new DiscoveredDrive(
                    Id: "tape0",
                    DevicePath: @"\\.\Tape0",
                    DisplayName: "Fake Tape0",
                    IsFake: true,
                    Drive: new FakeTapeDrive())
            ];
        }

        var drives = new List<DiscoveredDrive>();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return drives;

        for (var i = 0; i < MaxTapeDeviceCount; i++)
        {
            var devicePath = $@"\\.\Tape{i}";
            LTOTapeDrive? drive = null;

            try
            {
                drive = new LTOTapeDrive(devicePath, open: true);

                var displayName = devicePath;
                if (drive.GetInquiry())
                {
                    var vendor = drive.Vendor.Trim();
                    var product = drive.Product.Trim();
                    var details = string.Join(" ", new[] { vendor, product }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    if (!string.IsNullOrWhiteSpace(details))
                        displayName = details;
                }

                drives.Add(new DiscoveredDrive($"tape{i}", devicePath, displayName, false, drive));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Tape device probe failed for {DevicePath}", devicePath);
                drive?.Dispose();
            }
        }

        return drives;
    }

    public TapeDriveSnapshot GetSnapshot(string tapeDriveId)
    {
        var driveInfo = RefreshAndResolveDrive(tapeDriveId);
        var context = _contexts.AddOrUpdate(
            tapeDriveId,
            _ => new MachineContext
            {
                TapeDriveId = driveInfo.Id,
                DevicePath = driveInfo.DevicePath,
                IsFake = driveInfo.IsFake,
                State = TapeDriveState.Empty,
            },
            (_, old) =>
            {
                old.State = old.State == TapeDriveState.Unknown ? TapeDriveState.Empty : old.State;
                return old;
            });

        return ToSnapshot(context, null);
    }

    public TapeDriveSnapshot Execute(string tapeDriveId, TapeDriveAction action)
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
                    State = TapeDriveState.Empty,
                },
                (_, old) => old);

            EnsureActionAllowed(context.State, action, tapeDriveId);

            CartridgeMemory? cm = null;
            try
            {
                switch (action)
                {
                    case TapeDriveAction.ThreadTape:
                        drive.Load();
                        context.State = TapeDriveState.Threaded;
                        context.LastError = null;
                        break;
                    case TapeDriveAction.LoadTape:
                        drive.LoadUnthread();
                        context.State = TapeDriveState.Loaded;
                        context.LastError = null;
                        break;
                    case TapeDriveAction.UnthreadTape:
                        drive.Unthread();
                        context.State = TapeDriveState.Loaded;
                        context.LastError = null;
                        break;
                    case TapeDriveAction.EjectTape:
                        drive.Unload();
                        context.State = TapeDriveState.Empty;
                        context.LastError = null;
                        break;
                    case TapeDriveAction.ReadInfo:
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
                context.State = TapeDriveState.Faulted;
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
        var drives = ScanAndSync();
        var driveInfo = drives.FirstOrDefault(d => string.Equals(d.Id, tapeDriveId, StringComparison.OrdinalIgnoreCase));
        if (driveInfo is null)
        {
            throw new KeyNotFoundException($"Tape drive '{tapeDriveId}' was not found.");
        }

        return driveInfo;
    }

    private static void EnsureActionAllowed(TapeDriveState state, TapeDriveAction action, string tapeDriveId)
    {
        if (GetAllowedActions(state).Contains(action))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Action '{action}' is not allowed when tape drive '{tapeDriveId}' is in state '{state}'.");
    }

    private static IReadOnlyList<TapeDriveAction> GetAllowedActions(TapeDriveState state)
    {
        return state switch
        {
            TapeDriveState.Empty => [TapeDriveAction.ThreadTape, TapeDriveAction.LoadTape],
            TapeDriveState.Loaded => [TapeDriveAction.ThreadTape, TapeDriveAction.EjectTape, TapeDriveAction.ReadInfo],
            TapeDriveState.Threaded => [TapeDriveAction.UnthreadTape, TapeDriveAction.EjectTape, TapeDriveAction.ReadInfo],
            TapeDriveState.Faulted => [TapeDriveAction.EjectTape],
            _ => [TapeDriveAction.ThreadTape, TapeDriveAction.LoadTape],
        };
    }

    private static TapeDriveSnapshot ToSnapshot(MachineContext context, CartridgeMemory? cm)
    {
        return new TapeDriveSnapshot(
            context.TapeDriveId,
            context.DevicePath,
            context.State,
            GetAllowedActions(context.State),
            context.LastError,
            context.IsFake,
            cm);
    }

    private sealed record DiscoveredDrive(
        string Id,
        string DevicePath,
        string DisplayName,
        bool IsFake,
        TapeDriveBase Drive);
}
