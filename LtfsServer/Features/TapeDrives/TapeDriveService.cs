using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using Ltfs;

using LtfsServer.BootStrap;
using LtfsServer.Features.LocalTapes;
using LtfsServer.Features.Tasks;

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
        public required string DisplayName { get; set; }
        public required bool IsFake { get; init; }
        public CartridgeMemory? CartridgeMemory { get; set; }
        public string? LoadedBarcode { get; set; }
        public bool? HasLtfsFilesystem { get; set; }
        public string? LtfsVolumeName { get; set; }
        public Ltfs.Ltfs? LtfsContext { get; set; }
        public bool MediaContextLoaded { get; set; }
    }

    private const int MaxTapeDeviceCount = 32;
    private static readonly IReadOnlyList<TapeDriveAction> SupportedActions =
    [
        TapeDriveAction.ThreadTape,
        TapeDriveAction.LoadTape,
        TapeDriveAction.UnthreadTape,
        TapeDriveAction.EjectTape,
        TapeDriveAction.ReadInfo,
    ];

    private readonly ITapeDriveRegistry _registry;
    private readonly ILogger<TapeDriveService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ILocalTapeRegistry _localTapeRegistry;
    private readonly AppData _appData;
    private readonly object _sync = new();
    private readonly List<string> _managedIds = new();
    private readonly ConcurrentDictionary<string, MachineContext> _contexts = new();
    private readonly ConcurrentDictionary<string, object> _locks = new();
    private readonly ConcurrentDictionary<string, TapeDriveInfo> _driveInfos = new(StringComparer.OrdinalIgnoreCase);

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
            var discovered = DiscoverDrives();
            var discoveredIds = new HashSet<string>(discovered.Select(entry => entry.Id), StringComparer.OrdinalIgnoreCase);
            var results = new List<TapeDriveInfo>();

            foreach (var entry in discovered)
            {
                if (_registry.TryGet(entry.Id, out var existingDrive) && existingDrive is not null)
                {
                    (entry.Drive as IDisposable)?.Dispose();
                    var existingInfo = new TapeDriveInfo(entry.Id, entry.DevicePath, entry.DisplayName, entry.IsFake);
                    _driveInfos[entry.Id] = existingInfo;
                    UpdateOrCreateContext(existingInfo);
                    results.Add(existingInfo);
                    continue;
                }

                if (!_registry.TryAdd(entry.Id, entry.Drive))
                {
                    (entry.Drive as IDisposable)?.Dispose();
                    continue;
                }

                var info = new TapeDriveInfo(entry.Id, entry.DevicePath, entry.DisplayName, entry.IsFake);
                _driveInfos[entry.Id] = info;
                UpdateOrCreateContext(info);
                results.Add(info);
            }

            RemoveMissingDrives(discoveredIds);

            _managedIds.Clear();
            _managedIds.AddRange(results.Select(result => result.Id));

            return results;
        }
    }

    public Ltfs.Ltfs? GetCachedLtfsContext(string tapeDriveId)
    {
        if (_contexts.TryGetValue(tapeDriveId, out var context))
        {
            return context.LtfsContext;
        }

        return null;
    }

    public void UpdateCachedLtfsContext(string tapeDriveId, Ltfs.Ltfs? ltfs, string? loadedBarcode = null)
    {
        var context = GetOrCreateContext(tapeDriveId);
        lock (GetDriveLock(tapeDriveId))
        {
            context.LtfsContext = ltfs;
            context.MediaContextLoaded = true;
            context.HasLtfsFilesystem = ltfs is not null;
            context.LtfsVolumeName = ltfs?.LtfsIndexCurr?.Directory?.Name?.Value;
            if (!string.IsNullOrWhiteSpace(loadedBarcode))
            {
                context.LoadedBarcode = SanitizeBarcode(loadedBarcode);
            }
        }
    }

    public void ReleaseCachedLtfsContext(string tapeDriveId)
    {
        if (!_contexts.TryGetValue(tapeDriveId, out var context))
        {
            return;
        }

        lock (GetDriveLock(tapeDriveId))
        {
            context.LtfsContext = null;
        }
    }

    private void RemoveMissingDrives(HashSet<string> discoveredIds)
    {
        foreach (var id in _managedIds.Where(id => !discoveredIds.Contains(id)).ToArray())
        {
            _registry.TryRemove(id, out _);
            _contexts.TryRemove(id, out _);
            _locks.TryRemove(id, out _);
            _driveInfos.TryRemove(id, out _);
        }
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

        if (OperatingSystem.IsWindows())
            return DiscoverWindowsDrives();

        if (OperatingSystem.IsLinux())
            return DiscoverLinuxDrives();

        return [];
    }

    private List<DiscoveredDrive> DiscoverWindowsDrives()
    {
        var drives = new List<DiscoveredDrive>();

        for (var i = 0; i < MaxTapeDeviceCount; i++)
        {
            var devicePath = $@"\\.\Tape{i}";

            TryProbeDrive(devicePath, $"tape{i}", drives);
        }

        return drives;
    }

    private List<DiscoveredDrive> DiscoverLinuxDrives()
    {
        var drives = new List<DiscoveredDrive>();
        var devicePaths = EnumerateLinuxTapeDevicePaths().ToArray();

        foreach (var devicePath in devicePaths)
        {
            var driveId = BuildLinuxDriveId(devicePath);
            TryProbeDrive(devicePath, driveId, drives);
        }

        return drives;
    }

    private IEnumerable<string> EnumerateLinuxTapeDevicePaths()
    {
        const string devRoot = "/dev";

        if (!Directory.Exists(devRoot))
            yield break;

        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        string[] patterns = ["nst*", "st*"];

        foreach (var pattern in patterns)
        {
            IEnumerable<string> candidates;

            try
            {
                candidates = Directory.EnumerateFiles(devRoot, pattern, SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Tape device enumeration failed for pattern {Pattern}", pattern);
                continue;
            }

            foreach (var path in candidates.OrderBy(static path => path, StringComparer.Ordinal))
            {
                if (seenPaths.Add(path))
                    yield return path;
            }
        }
    }

    private void TryProbeDrive(string devicePath, string driveId, ICollection<DiscoveredDrive> drives)
    {
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

            drives.Add(new DiscoveredDrive(driveId, devicePath, displayName, false, drive));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Tape device probe failed for {DevicePath}", devicePath);
            drive?.Dispose();
        }
    }

    private static string BuildLinuxDriveId(string devicePath)
    {
        var fileName = Path.GetFileName(devicePath);
        if (string.IsNullOrWhiteSpace(fileName))
            return "tape-linux";

        var numericSuffix = Regex.Match(fileName, "(\\d+)$");
        if (numericSuffix.Success)
            return $"tape{numericSuffix.Groups[1].Value}";

        return $"tape-{fileName.ToLowerInvariant()}";
    }

    public TapeDriveSnapshot GetSnapshot(string tapeDriveId)
    {
        var driveInfo = ResolveDrive(tapeDriveId);
        if (!_registry.TryGet(tapeDriveId, out var drive) || drive is null)
        {
            throw new InvalidOperationException($"Tape drive '{tapeDriveId}' is unavailable.");
        }

        var context = UpdateOrCreateContext(driveInfo);

        EnsureMediaContextLoaded(context, drive);
        return ToSnapshot(context);
    }

    public TapeDriveSnapshot Execute(string tapeDriveId, TapeDriveAction action)
    {
        var lockObj = _locks.GetOrAdd(tapeDriveId, _ => new object());

        lock (lockObj)
        {
            var driveInfo = ResolveDrive(tapeDriveId);
            if (!_registry.TryGet(tapeDriveId, out var drive) || drive is null)
            {
                throw new InvalidOperationException($"Tape drive '{tapeDriveId}' is unavailable.");
            }

            var context = UpdateOrCreateContext(driveInfo);

            try
            {
                switch (action)
                {
                    case TapeDriveAction.ThreadTape:
                        drive.Load();
                        InvalidateMediaContext(context, clearCartridgeMemory: false);
                        break;
                    case TapeDriveAction.LoadTape:
                        drive.LoadUnthread();
                        InvalidateMediaContext(context, clearCartridgeMemory: false);
                        break;
                    case TapeDriveAction.UnthreadTape:
                        drive.Unthread();
                        InvalidateMediaContext(context, clearCartridgeMemory: false);
                        break;
                    case TapeDriveAction.EjectTape:
                        drive.Unload();
                        InvalidateMediaContext(context, clearCartridgeMemory: true);
                        break;
                    case TapeDriveAction.ReadInfo:
                        var raw = drive.ReadDiagCM();
                        var cm = new CartridgeMemory();
                        cm.FromBytes(raw);
                        context.CartridgeMemory = cm;
                        context.LoadedBarcode = ResolveBarcode(drive, cm);
                        SaveCmBinaryAndUpdateRegistry(raw, drive, cm);
                        context.MediaContextLoaded = true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown tape action.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tape action {Action} failed for {TapeDriveId}", action, tapeDriveId);
                throw;
            }

            EnsureMediaContextLoaded(context, drive, forceRefresh: true);
            return ToSnapshot(context);
        }
    }

    public TapeDriveSnapshot Format(string tapeDriveId, FormatParam? formatParam)
    {
        var lockObj = _locks.GetOrAdd(tapeDriveId, _ => new object());

        lock (lockObj)
        {
            var driveInfo = ResolveDrive(tapeDriveId);
            if (!_registry.TryGet(tapeDriveId, out var drive) || drive is null)
            {
                throw new InvalidOperationException($"Tape drive '{tapeDriveId}' is unavailable.");
            }

            var context = UpdateOrCreateContext(driveInfo);
            EnsureMediaContextLoaded(context, drive);

            if (ResolveSnapshotState(context) == TapeDriveState.Empty)
            {
                throw new InvalidOperationException($"Tape drive '{tapeDriveId}' does not have a loaded tape.");
            }

            try
            {
                drive.Load();

                var fallbackBarcode = !string.IsNullOrWhiteSpace(context.LoadedBarcode)
                    ? context.LoadedBarcode
                    : tapeDriveId;
                var effectiveFormatParam = FormatTaskDefaults.NormalizeFormatParam(
                    formatParam,
                    fallbackBarcode,
                    context.LtfsVolumeName ?? fallbackBarcode);

                var ltfs = new Ltfs.Ltfs();
                ltfs.SetTapeDrive(drive);
                ltfs.LocalIndexRootPath = Path.Combine(_appData.Path, "local");
                ltfs.ExtraPartitionCount = effectiveFormatParam.ExtraPartitionCount;
                ltfs.Format(effectiveFormatParam);

                context.LoadedBarcode = effectiveFormatParam.Barcode;
                context.HasLtfsFilesystem = true;
                context.LtfsVolumeName = effectiveFormatParam.VolumeName;
                context.LtfsContext = ltfs;
                context.MediaContextLoaded = true;

                RefreshLocalIndexRegistry(effectiveFormatParam.Barcode);

                TryRefreshCartridgeMemory(context, drive);

                return ToSnapshot(context);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tape format failed for {TapeDriveId}", tapeDriveId);
                throw;
            }
        }
    }

    private void EnsureMediaContextLoaded(MachineContext context, TapeDriveBase drive, bool forceRefresh = false)
    {
        if (context.MediaContextLoaded && !forceRefresh)
        {
            return;
        }

        RefreshMediaContextCore(context, drive);
    }

    private void RefreshMediaContextCore(MachineContext context, TapeDriveBase drive)
    {
        context.CartridgeMemory = null;
        context.LoadedBarcode = null;
        context.LtfsContext = null;
        context.HasLtfsFilesystem = null;
        context.LtfsVolumeName = null;

        try
        {
            var barcode = SanitizeBarcode(drive.ReadBarCode());
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                context.LoadedBarcode = barcode;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ReadBarCode failed for {TapeDriveId}", context.TapeDriveId);
        }

        try
        {
            var raw = drive.ReadDiagCM();
            if (raw.Length > 0)
            {
                var cm = new CartridgeMemory();
                cm.FromBytes(raw);
                context.CartridgeMemory = cm;
                var barcode = ResolveBarcode(drive, cm);
                if (!string.IsNullOrWhiteSpace(barcode))
                {
                    context.LoadedBarcode = barcode;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ReadDiagCM failed for {TapeDriveId}", context.TapeDriveId);
        }

        try
        {
            var ltfs = new Ltfs.Ltfs();
            ltfs.SetTapeDrive(drive);
            var hasLtfs = ltfs.ReadLtfs();
            context.HasLtfsFilesystem = hasLtfs;
            context.LtfsVolumeName = hasLtfs
                ? ltfs.LtfsIndexCurr?.Directory?.Name?.Value
                : null;
            context.LtfsContext = hasLtfs ? ltfs : null;
        }
        catch (Exception ex)
        {
            context.HasLtfsFilesystem = null;
            context.LtfsVolumeName = null;
            context.LtfsContext = null;
            _logger.LogDebug(ex, "ReadLtfs probe failed for {TapeDriveId}", context.TapeDriveId);
        }

        context.MediaContextLoaded = true;
    }

    private static void InvalidateMediaContext(MachineContext context, bool clearCartridgeMemory)
    {
        if (clearCartridgeMemory)
        {
            context.CartridgeMemory = null;
        }

        context.LoadedBarcode = clearCartridgeMemory ? null : context.LoadedBarcode;
        context.HasLtfsFilesystem = null;
        context.LtfsVolumeName = null;
        context.LtfsContext = null;
        context.MediaContextLoaded = false;
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

    private void TryRefreshCartridgeMemory(MachineContext context, TapeDriveBase drive)
    {
        try
        {
            var raw = drive.ReadDiagCM();
            if (raw.Length == 0)
            {
                return;
            }

            var cm = new CartridgeMemory();
            cm.FromBytes(raw);
            context.CartridgeMemory = cm;

            SaveCmBinaryAndUpdateRegistry(raw, drive, cm);

            var barcode = ResolveBarcode(drive, cm);
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                context.LoadedBarcode = barcode;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ReadDiagCM refresh failed for {TapeDriveId}", context.TapeDriveId);
        }
    }

    private void RefreshLocalIndexRegistry(string tapeBarcode)
    {
        if (string.IsNullOrWhiteSpace(tapeBarcode))
            return;

        var safeBarcode = SanitizeBarcode(tapeBarcode);
        var indexDirectory = Path.Combine(_appData.Path, "local", safeBarcode);
        if (!Directory.Exists(indexDirectory))
            return;

        foreach (var filePath in Directory.EnumerateFiles(indexDirectory, "*.xml", SearchOption.TopDirectoryOnly))
        {
            _ = _localTapeRegistry.TryUpsertFile(safeBarcode, filePath);
        }
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

    private TapeDriveInfo ResolveDrive(string tapeDriveId)
    {
        if (_driveInfos.TryGetValue(tapeDriveId, out var cachedInfo))
        {
            return cachedInfo;
        }

        var drives = ScanAndSync();
        var discoveredInfo = drives.FirstOrDefault(d => string.Equals(d.Id, tapeDriveId, StringComparison.OrdinalIgnoreCase));
        if (discoveredInfo is null)
        {
            throw new KeyNotFoundException($"Tape drive '{tapeDriveId}' was not found.");
        }

        return discoveredInfo;
    }

    private MachineContext GetOrCreateContext(string tapeDriveId)
    {
        var driveInfo = ResolveDrive(tapeDriveId);
        return UpdateOrCreateContext(driveInfo);
    }

    private MachineContext UpdateOrCreateContext(TapeDriveInfo driveInfo)
    {
        return _contexts.AddOrUpdate(
            driveInfo.Id,
            _ => new MachineContext
            {
                TapeDriveId = driveInfo.Id,
                DevicePath = driveInfo.DevicePath,
                DisplayName = driveInfo.DisplayName,
                IsFake = driveInfo.IsFake,
            },
            (_, old) =>
            {
                old.DisplayName = driveInfo.DisplayName;
                return old;
            });
    }

    private object GetDriveLock(string tapeDriveId) => _locks.GetOrAdd(tapeDriveId, _ => new object());

    private static TapeDriveState ResolveSnapshotState(MachineContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.LoadedBarcode)
            || context.CartridgeMemory is not null
            || context.HasLtfsFilesystem.HasValue)
        {
            return TapeDriveState.Loaded;
        }

        return TapeDriveState.Empty;
    }

    private static TapeDriveSnapshot ToSnapshot(MachineContext context)
    {
        return new TapeDriveSnapshot(
            context.TapeDriveId,
            context.DevicePath,
            ResolveSnapshotState(context),
            SupportedActions,
            null,
            context.IsFake,
            context.CartridgeMemory,
            context.LoadedBarcode,
            context.HasLtfsFilesystem,
            context.LtfsVolumeName);
    }

    private sealed record DiscoveredDrive(
        string Id,
        string DevicePath,
        string DisplayName,
        bool IsFake,
        TapeDriveBase Drive);
}
