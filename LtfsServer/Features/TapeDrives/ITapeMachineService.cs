using LtoTape;

namespace LtfsServer.Features.TapeDrives;

public enum TapeMachineState
{
    Unknown,
    Empty,
    Loaded,
    Threaded,
    Faulted,
}

public enum TapeMachineAction
{
    ThreadTape,
    LoadTape,
    UnthreadTape,
    EjectTape,
    ReadInfo,
}

public sealed record TapeMachineSnapshot(
    string TapeDriveId,
    string DevicePath,
    TapeMachineState State,
    IReadOnlyList<TapeMachineAction> AllowedActions,
    string? LastError,
    bool IsFake,
    CartridgeMemory? CartridgeMemory);

public interface ITapeMachineService
{
    TapeMachineSnapshot GetSnapshot(string tapeDriveId);
    TapeMachineSnapshot Execute(string tapeDriveId, TapeMachineAction action);
}
