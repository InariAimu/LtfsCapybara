using Microsoft.AspNetCore.Builder;
using LtfsServer.Services;

namespace LtfsServer.API;

public static class APILocalTapes
{
    public static void MapLocalTapesApi(this WebApplication app)
    {
        app.MapGet("/api/localtapes", (ILocalTapeRegistry registry) =>
            Results.Ok(registry.GetTapeNames()));
    }
}
