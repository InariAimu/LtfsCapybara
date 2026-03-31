using System.Collections.Generic;

namespace LtfsServer.Features.TapeDrives;

public interface ITapeDriveService
{
    IReadOnlyList<TapeDriveInfo> ScanAndSync();
}
