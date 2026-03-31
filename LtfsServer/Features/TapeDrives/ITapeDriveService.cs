using System.Collections.Generic;

using LtoTape;

namespace LtfsServer.Features.TapeDrives;

public enum TapeDriveState
{
    Unknown,
    Empty,
    Loaded,
    Threaded,
    Faulted,
}

public enum TapeDriveAction
{
    ThreadTape,
    LoadTape,
    UnthreadTape,
    EjectTape,
    ReadInfo,
}

public sealed record TapeDriveSnapshot(
    string TapeDriveId,
    string DevicePath,
    TapeDriveState State,
    IReadOnlyList<TapeDriveAction> AllowedActions,
    string? LastError,
    bool IsFake,
    CartridgeMemory? CartridgeMemory);

public interface ITapeDriveService
{
    IReadOnlyList<TapeDriveInfo> ScanAndSync();
    TapeDriveSnapshot GetSnapshot(string tapeDriveId);
    TapeDriveSnapshot Execute(string tapeDriveId, TapeDriveAction action);
}
