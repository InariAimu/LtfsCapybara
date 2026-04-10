using System.Collections.Generic;

using Ltfs;

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
    CartridgeMemory? CartridgeMemory,
    string? LoadedBarcode,
    bool? HasLtfsFilesystem,
    string? LtfsVolumeName);

public sealed class FormatMountedTapeRequest
{
    public FormatParam? FormatParam { get; set; }
}

public interface ITapeDriveService
{
    IReadOnlyList<TapeDriveInfo> ScanAndSync();
    TapeDriveSnapshot GetSnapshot(string tapeDriveId);
    TapeDriveSnapshot Execute(string tapeDriveId, TapeDriveAction action);
    TapeDriveSnapshot Format(string tapeDriveId, FormatParam? formatParam);
    Ltfs.Ltfs? GetCachedLtfsContext(string tapeDriveId);
    void UpdateCachedLtfsContext(string tapeDriveId, Ltfs.Ltfs? ltfs, string? loadedBarcode = null);
    void ReleaseCachedLtfsContext(string tapeDriveId);
}
