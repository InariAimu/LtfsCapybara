namespace LtfsServer.Features.ServerSettings;

public static class APIServerSettings
{
    public static void MapServerSettingsApi(this WebApplication app)
    {
        app.MapGet("/api/settings/server", (IServerSettingsService serverSettingsService) =>
            Results.Ok(serverSettingsService.Get()));

        app.MapPut("/api/settings/server", (ServerSettingsUpdateRequest request, IServerSettingsService serverSettingsService) =>
        {
            try
            {
                return Results.Ok(serverSettingsService.Save(request));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }
}