using Microsoft.AspNetCore.Builder;

namespace LtfsServer.Features.TapeDrives;

public static class APITapeDrive
{
    public static void MapTapeDriveApi(this WebApplication app)
    {
        app.MapGet("/api/tapedrives", (ITapeDriveService tapeDriveService) =>
            Results.Ok(tapeDriveService.ScanAndSync()));

        app.MapGet("/api/tapedrives/{id}/machine", (string id, ITapeMachineService tapeMachineService) =>
        {
            try
            {
                return Results.Ok(tapeMachineService.GetSnapshot(id));
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

        app.MapPost("/api/tapedrives/{id}/machine/{action}", (string id, string action, ITapeMachineService tapeMachineService) =>
        {
            if (!TryParseAction(action, out var parsedAction))
            {
                return Results.BadRequest(new { message = $"Unsupported tape machine action '{action}'." });
            }

            try
            {
                return Results.Ok(tapeMachineService.Execute(id, parsedAction));
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

    private static bool TryParseAction(string action, out TapeMachineAction parsedAction)
    {
        parsedAction = action.ToLowerInvariant() switch
        {
            "thread" => TapeMachineAction.ThreadTape,
            "load" => TapeMachineAction.LoadTape,
            "unthread" => TapeMachineAction.UnthreadTape,
            "eject" => TapeMachineAction.EjectTape,
            "read-info" => TapeMachineAction.ReadInfo,
            _ => (TapeMachineAction)(-1),
        };

        return parsedAction is >= TapeMachineAction.ThreadTape and <= TapeMachineAction.ReadInfo;
    }
}
