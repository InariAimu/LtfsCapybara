using System.Collections.Generic;
using TapeDrive;

namespace LtfsServer.Features.TapeDrives;

public sealed record TapeDriveInfo(string Id, string DevicePath, string DisplayName, bool IsFake);

public interface ITapeDriveRegistry
{
    bool TryAdd(string id, TapeDriveBase drive);
    bool TryRemove(string id, out TapeDriveBase? drive);
    bool TryGet(string id, out TapeDriveBase? drive);
    IReadOnlyCollection<TapeDriveBase> GetAll();
    int Count { get; }
}
