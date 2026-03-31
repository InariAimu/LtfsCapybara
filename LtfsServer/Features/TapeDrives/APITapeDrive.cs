using Microsoft.AspNetCore.Builder;

namespace LtfsServer.Features.TapeDrives;

public static class APITapeDrive
{
    public static void MapTapeDriveApi(this WebApplication app)
    {
        app.MapGet("/api/tapedrives", (ITapeDriveService tapeDriveService) =>
            Results.Ok(tapeDriveService.ScanAndSync()));

        app.MapGet("/api/tapedrives/{id}/machine", (string id, ITapeDriveService tapeDriveService) =>
        {
            try
            {
                return Results.Ok(tapeDriveService.GetSnapshot(id));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        app.MapPost("/api/tapedrives/{id}/machine/{action}", (string id, string action, ITapeDriveService tapeDriveService) =>
        {
            if (!TryParseAction(action, out var parsedAction))
            {
                return Results.BadRequest(new { message = $"Unsupported tape machine action '{action}'." });
            }

            try
            {
                return Results.Ok(tapeDriveService.Execute(id, parsedAction));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });
    }

    private static bool TryParseAction(string action, out TapeDriveAction parsedAction)
    {
        parsedAction = action.ToLowerInvariant() switch
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
