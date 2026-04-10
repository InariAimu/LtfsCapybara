namespace LtfsServer.Features.Overview;

public static class APIOverview
{
    public static void MapOverviewApi(this WebApplication app)
    {
        app.MapGet("/api/overview", (IOverviewService overviewService) =>
            Results.Ok(overviewService.GetSnapshot()));
    }
}
