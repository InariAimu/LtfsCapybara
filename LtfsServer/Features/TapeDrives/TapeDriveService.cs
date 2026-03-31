using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using TapeDrive;

namespace LtfsServer.Features.TapeDrives;

public class TapeDriveService : ITapeDriveService
{
    private const int MaxTapeDeviceCount = 32;

    private readonly ITapeDriveRegistry _registry;
    private readonly ILogger<TapeDriveService> _logger;
    private readonly IConfiguration _configuration;
    private readonly object _sync = new();
    private readonly List<string> _managedIds = new();

    public TapeDriveService(ITapeDriveRegistry registry, ILogger<TapeDriveService> logger, IConfiguration configuration)
    {
        _registry = registry;
        _logger = logger;
        _configuration = configuration;
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

    private sealed record DiscoveredDrive(
        string Id,
        string DevicePath,
        string DisplayName,
        bool IsFake,
        TapeDriveBase Drive);
}
