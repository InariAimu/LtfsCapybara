using System.Collections.Generic;
using TapeDrive;

namespace LtfsServer.Services;

public interface ITapeDriveRegistry
{
    bool TryAdd(string id, TapeDriveBase drive);
    bool TryRemove(string id, out TapeDriveBase? drive);
    bool TryGet(string id, out TapeDriveBase? drive);
    IReadOnlyCollection<TapeDriveBase> GetAll();
    int Count { get; }
}
