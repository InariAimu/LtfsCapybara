using Microsoft.AspNetCore.Builder;
using LtfsServer.Services;
using TapeDrive;

namespace LtfsServer.API;

public static class APITapeDrive
{
    public static void MapTapeDriveApi(this WebApplication app)
    {
        // Placeholder endpoint for tape drive info
        app.MapGet("/apt/tapdrive/info", () => Results.Ok(new
        {
            message = "TapeDrive info endpoint (placeholder)",
            implemented = false
        }))
        .WithName("GetTapeDriveInfo");

        app.MapGet("/api/tapedrives", (ITapeDriveRegistry registry) =>
            Results.Ok(registry.GetAll().Select(d => new { name = d.GetType().Name })));
    }
}
