using Microsoft.AspNetCore.Builder;
using LtfsServer.Services;
using TapeDrive;

namespace LtfsServer.API;

public static class APITapeDrive
{
    public static void MapTapeDriveApi(this WebApplication app)
    {
        app.MapGet("/api/tapedrives", (ITapeDriveRegistry registry) =>
            Results.Ok(registry.GetAll().Select(d => new { name = d.GetType().Name })));
    }
}
