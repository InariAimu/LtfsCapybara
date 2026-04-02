using LtfsServer.Features.TapeDrives;
using System.Text.Json;

namespace LtfsServer.Features.AI.Tools;

[AIToolModule(Name = "tapedrive", Description = "Tools for tape drive operations")]
public sealed class TapeDriveOperationTool
{
    private readonly ITapeDriveService _tapeDriveService;

    public TapeDriveOperationTool(ITapeDriveService tapeDriveService)
    {
        _tapeDriveService = tapeDriveService;
    }

    [AITool(Name = "tapedrive_operation", Description = "Operate tapedrive, and get operation result. the operation type is one of 4: [load | eject | thread | unthread] (load: load tape cartridge only. thread: load cartridge if unload, then load actual tape. unthread: unload tape, but keep cartridge. eject: unload tape if loaded, then eject cartridge.)")]
    public Task<string> ExecuteAsync(
        [AIToolParam(Description = "tapedrive operation type.")] string operation,
        CancellationToken cancellationToken,
        [AIToolParam(Description = "tapedrive id. Optional when exactly one tapedrive is available.")] string tapeDriveId = "")
    {
        cancellationToken.ThrowIfCancellationRequested();

        operation = (operation ?? string.Empty).Trim();

        if (!TapeDriveActionParser.TryParseAction(operation, out var action)
            || action == TapeDriveAction.ReadInfo)
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                ok = false,
                error = "Unsupported operation"
            }));
        }

        var drives = _tapeDriveService.ScanAndSync();
        var resolvedTapeDriveId = ResolveTapeDriveId(drives, tapeDriveId);

        Console.WriteLine($"Executing tape drive operation '{operation}' on tape drive '{resolvedTapeDriveId}'...");
        var snapshot = _tapeDriveService.Execute(resolvedTapeDriveId, action);

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            ok = true,
            tapeDriveId = resolvedTapeDriveId,
            operation,
            info = new {
                allowedActions = snapshot.AllowedActions.Select(a => TapeDriveActionToOperation(a)).ToArray(),
            },
        }));
    }

    private static string ResolveTapeDriveId(IReadOnlyList<TapeDriveInfo> drives, string tapeDriveId)
    {
        var normalizedTapeDriveId = (tapeDriveId ?? string.Empty).Trim();

        if (!string.IsNullOrWhiteSpace(normalizedTapeDriveId))
        {
            var matchedDrive = drives.FirstOrDefault(d => string.Equals(d.Id, normalizedTapeDriveId, StringComparison.OrdinalIgnoreCase));
            if (matchedDrive is null)
            {
                throw new KeyNotFoundException($"Tape drive '{normalizedTapeDriveId}' was not found.");
            }

            return matchedDrive.Id;
        }

        return drives.Count switch
        {
            0 => throw new KeyNotFoundException("No tape drives were found."),
            1 => drives[0].Id,
            _ => throw new InvalidOperationException("Multiple tape drives are available. Provide tapeDriveId.")
        };
    }

    public static string TapeDriveActionToOperation(TapeDriveAction action)
    {
        return action switch
        {
            TapeDriveAction.ThreadTape => "thread",
            TapeDriveAction.LoadTape => "load",
            TapeDriveAction.UnthreadTape => "unthread",
            TapeDriveAction.EjectTape => "eject",
            _ => throw new ArgumentException($"Unsupported tape drive action '{action}'."),
        };
    }
}
