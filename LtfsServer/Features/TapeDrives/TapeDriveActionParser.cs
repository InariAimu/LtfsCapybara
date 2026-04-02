namespace LtfsServer.Features.TapeDrives;

public static class TapeDriveActionParser
{
    public static bool TryParseAction(string action, out TapeDriveAction parsedAction)
    {
        parsedAction = (action ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "thread" => TapeDriveAction.ThreadTape,
            "load" => TapeDriveAction.LoadTape,
            "unthread" => TapeDriveAction.UnthreadTape,
            "eject" => TapeDriveAction.EjectTape,
            "read-info" => TapeDriveAction.ReadInfo,
            _ => (TapeDriveAction)(-1),
        };

        return parsedAction is >= TapeDriveAction.ThreadTape and <= TapeDriveAction.ReadInfo;
    }
}