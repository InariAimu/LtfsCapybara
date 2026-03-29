using LtfsServer.Services;

namespace LtfsServer.API;

public static class APILocalFileSystem
{
    public static void MapLocalFileSystemApi(this WebApplication app)
    {
        app.MapGet("/api/fsroots", async (ILocalFileSystemTreeService service, HttpContext context) =>
        {
            var roots = await service.GetRootsAsync(context.RequestAborted);
            return Results.Ok(new
            {
                items = roots.Select(ToDto).ToArray(),
                loadedAtUtc = DateTime.UtcNow,
            });
        });

        app.MapGet("/api/fschildren", async (string? path, ILocalFileSystemTreeService service, HttpContext context) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Results.BadRequest(new { error = "Query string 'path' is required." });
            }

            try
            {
                var result = await service.GetChildrenAsync(path, context.RequestAborted);
                return Results.Ok(new
                {
                    parentPath = result.ParentPath,
                    items = result.Children.Select(ToDto).ToArray(),
                    warning = result.Warning,
                    loadedAtUtc = DateTime.UtcNow,
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (DirectoryNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });
    }

    private static object ToDto(LocalFsNode node)
    {
        return new
        {
            id = node.Id,
            name = node.Name,
            path = node.Path,
            kind = node.Kind,
            available = node.Available,
            hasChildren = node.HasChildren,
            error = node.Error,
        };
    }
}
