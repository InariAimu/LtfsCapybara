using System.Collections.Generic;

namespace LtfsServer.Services;

public interface ITapeDriveService
{
    IReadOnlyList<TapeDriveInfo> ScanAndSync();
}
