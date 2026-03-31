using Microsoft.AspNetCore.Builder;

namespace LtfsServer.Features.LocalTapes;

public static class APILocalTapes
{
    public static void MapLocalTapesApi(this WebApplication app)
    {
        app.MapGet("/api/localtapes", (ILocalTapeRegistry registry) =>
            Results.Ok(registry.GetTapeSummaries()));
    }
}
