using System.IO;

using TapeDrive.SCSICommands.LogSensePages;

namespace TapeDrive;

public enum TapeDriveIncidentSource
{
    LocalFileSystem,
    Win32Error,
    ScsiStatus,
    Sense,
    TapeAlert,
}

public enum TapeDriveIncidentSeverity
{
    Information,
    Warning,
    Critical,
}

public enum TapeDriveIncidentAction
{
    NotifyOnly,
    PauseCurrentTasks,
    StopAllOperations,
}

public enum TapeDriveIncidentResolution
{
    Continue,
    Abort,
}

public sealed class TapeDriveIncident
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required TapeDriveIncidentSource Source { get; init; }
    public required TapeDriveIncidentSeverity Severity { get; init; }
    public required TapeDriveIncidentAction Action { get; init; }
    public required string Message { get; init; }
    public string? Detail { get; init; }
    public int? Win32ErrorCode { get; init; }
    public ushort? ScsiStatus { get; init; }
    public SenseInfo? SenseInfo { get; init; }
    public IReadOnlyList<int> TapeAlertIndexes { get; init; } = Array.Empty<int>();
    public IReadOnlyList<TapeAlertItem> TapeAlerts { get; init; } = Array.Empty<TapeAlertItem>();
    public byte[]? Cdb { get; init; }
}

public sealed class TapeDriveIncidentEventArgs(TapeDriveIncident incident) : EventArgs
{
    public TapeDriveIncident Incident { get; } = incident;
}

public sealed class TapeDriveCommandException : IOException
{
    public TapeDriveCommandException(TapeDriveIncident incident)
        : base(BuildMessage(incident))
    {
        Incident = incident;
    }

    public TapeDriveIncident Incident { get; }

    private static string BuildMessage(TapeDriveIncident incident)
    {
        var source = incident.Source.ToString();
        var severity = incident.Severity.ToString();
        var message = $"{source} {severity}: {incident.Message}";

        if (!string.IsNullOrWhiteSpace(incident.Detail))
            return $"{message} {incident.Detail}";

        return message;
    }
}