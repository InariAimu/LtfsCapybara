using Microsoft.AspNetCore.Builder;
using Ltfs;
using Ltfs.Index;
using LtoTape;
using LtfsServer.Features.LocalTapes;
using LtfsServer.Features.Tasks;
using LtfsServer.BootStrap;

namespace LtfsServer.Features.LocalIndex;

public static class APILocalIndex
{
    public static void MapLocalIndexApi(this WebApplication app)
    {
        app.MapGet("/api/local/{tapeName}", (string tapeName, ILocalIndexQueryService queryService) =>
        {
            return ToResult(queryService.GetDirectory(tapeName, "/"));
        });

        app.MapGet("/api/local/{tapeName}/{**path}", (string tapeName, string path, ILocalIndexQueryService queryService) =>
        {
            return ToResult(queryService.GetDirectory(tapeName, path));
        });

        app.MapDelete("/api/local/{tapeName}", (string tapeName, ILocalTapeRegistry registry, ITaskGroupService taskService, AppData appData) =>
        {
            return DeleteLocalIndexPathDto(tapeName, "/", registry, taskService, appData);
        });

        app.MapDelete("/api/local/{tapeName}/{**path}", (string tapeName, string path, ILocalTapeRegistry registry, ITaskGroupService taskService, AppData appData) =>
        {
            return DeleteLocalIndexPathDto(tapeName, path, registry, taskService, appData);
        });

        app.MapGet("/api/localcm/{tapeName}", (string tapeName, ILocalTapeRegistry registry, AppData appData) =>
        {
            var file = registry.GetFiles(tapeName)
                .Where(HasCartridgeMemory)
                .OrderByDescending(f => f.Index.Ticks)
                .FirstOrDefault();

            if (file is null)
                return Results.NotFound(new { error = "No cartridge memory files found for tape" });

            var cmPath = Path.Combine(appData.Path, "local", tapeName, file.Index.FileName);
            if (!File.Exists(cmPath))
                return Results.NotFound(new { error = "Cartridge memory file not found" });

            try
            {
                var cartridgeMemory = new CartridgeMemory();
                if (cmPath.EndsWith(".cmbin", StringComparison.OrdinalIgnoreCase))
                    cartridgeMemory.FromBinaryFile(cmPath);
                else
                    cartridgeMemory.FromLcgCmFile(cmPath);
                return Results.Ok(CartridgeMemoryDto.From(cartridgeMemory));
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    title: "Failed to parse cartridge memory file",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }

    private static IResult ToResult(LocalIndexQueryResponse response)
    {
        return response.StatusCode switch
        {
            StatusCodes.Status200OK => Results.Ok(response.Payload),
            StatusCodes.Status404NotFound => Results.NotFound(response.Payload),
            StatusCodes.Status400BadRequest => Results.BadRequest(response.Payload),
            _ => Results.Json(response.Payload, statusCode: response.StatusCode)
        };
    }

    private static IResult DeleteLocalIndexPathDto(
        string tapeName,
        string requestedPath,
        ILocalTapeRegistry registry,
        ITaskGroupService taskService,
        AppData appData)
    {
        var normalizedPath = LocalIndexPath.NormalizePath(requestedPath);

        LtfsDirectory? root = null;
        var file = registry.GetFiles(tapeName)
            .Where(HasXmlIndex)
            .OrderByDescending(f => f.Index.Ticks)
            .FirstOrDefault();

        if (file is not null)
        {
            var indexPath = Path.Combine(appData.Path, "local", tapeName, file.Index.FileName);
            var index = LtfsIndex.FromXmlFile(indexPath);
            root = index?.Directory;
        }

        var targetDir = root is null
            ? null
            : normalizedPath == "/" ? root : LocalIndexPath.FindDirectoryByPath(root, normalizedPath);

        try
        {
            var updatedGroup = taskService.DeleteLocalIndexPath(tapeName, normalizedPath, targetDir);
            return Results.Ok(updatedGroup);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static bool HasXmlIndex(TapeFileInfo file)
    {
        return file.Index.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCartridgeMemory(TapeFileInfo file)
    {
        return file.Index.FileName.EndsWith(".cm", StringComparison.OrdinalIgnoreCase)
            || file.Index.FileName.EndsWith(".cmbin", StringComparison.OrdinalIgnoreCase);
    }

}
