using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TapeDrive;

namespace LtfsServer.Features.TapeDrives;

public class TapeDriveRegistry : ITapeDriveRegistry, IDisposable
{
    private readonly ConcurrentDictionary<string, TapeDriveBase> _drives = new();

    public bool TryAdd(string id, TapeDriveBase drive) => _drives.TryAdd(id, drive);

    public bool TryRemove(string id, out TapeDriveBase? drive)
    {
        var removed = _drives.TryRemove(id, out drive);
        if (removed && drive is IDisposable d)
            d.Dispose();
        return removed;
    }

    public bool TryGet(string id, out TapeDriveBase? drive) => _drives.TryGetValue(id, out drive);

    public IReadOnlyCollection<TapeDriveBase> GetAll() => _drives.Values.ToArray();

    public int Count => _drives.Count;

    public void Dispose()
    {
        foreach (var kv in _drives)
        {
            try
            {
                (kv.Value as IDisposable)?.Dispose();
            }
            catch { }
        }
        _drives.Clear();
    }
}
