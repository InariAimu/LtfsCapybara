using Microsoft.AspNetCore.Builder;
using LtfsServer.Services;
using Ltfs;
using Ltfs.Index;
using LtoTape;

namespace LtfsServer.API;

public static class APILocalIndex
{
    public static void MapLocalIndexApi(this WebApplication app)
    {
        app.MapGet("/api/local/{tapeName}", (string tapeName, ILocalTapeRegistry registry, AppData appData) =>
        {
            var files = registry.GetFiles(tapeName)
                .Where(HasXmlIndex)
                .OrderByDescending(f => f.Index.Ticks)
                .ToArray();
            if (files.Length == 0)
                return Results.NotFound(new { error = "No index files found for tape" });

            var file = files[0];
            var path = Path.Combine(appData.Path, "local", tapeName, file.Index.FileName);
            var index = LtfsIndex.FromXmlFile(path);
            if (index is null)
                return Results.StatusCode(500);

            var dto = DirectoryToDto(index.Directory);
            return Results.Ok(dto);
        });

        app.MapGet("/api/local/{tapeName}/{**path}", (string tapeName, string path, ILocalTapeRegistry registry, AppData appData) =>
        {
            var files = registry.GetFiles(tapeName)
                .Where(HasXmlIndex)
                .OrderByDescending(f => f.Index.Ticks)
                .ToArray();
            if (files.Length == 0)
                return Results.NotFound(new { error = "No index files found for tape" });

            var file = files[0];
            var filePath = Path.Combine(appData.Path, "local", tapeName, file.Index.FileName);
            var index = LtfsIndex.FromXmlFile(filePath);
            if (index is null)
                return Results.StatusCode(500);

            var target = FindDirectoryByPath(index.Directory, path);
            if (target is null)
                return Results.NotFound(new { error = "Path not found" });

            return Results.Ok(DirectoryToDto(target));
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
                return Results.Ok(cartridgeMemory);
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

    private static bool HasXmlIndex(TapeFileInfo file)
    {
        return file.Index.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCartridgeMemory(TapeFileInfo file)
    {
        return file.Index.FileName.EndsWith(".cm", StringComparison.OrdinalIgnoreCase)
            || file.Index.FileName.EndsWith(".cmbin", StringComparison.OrdinalIgnoreCase);
    }

    private static object DirectoryToDto(LtfsDirectory dir)
    {
        var items = dir.Contents.Select(item =>
        {
            if (item is LtfsFile f)
            {
                return new
                {
                    type = "file",
                    name = f.Name.GetName(),
                    size = f.Length,
                    index = f.FileUID,
                    crc64 = f.ExtendedAttributes?["ltfs.hash.sha1sum"] ?? "",
                    createTime = f.CreationTime.ToString(),
                    modifyTime = f.ModifyTime.ToString(),
                    updateTime = f.ChangeTime.ToString(),
                    backupTime = f.BackupTime.ToString(),
                } as object;
            }
            else if (item is LtfsDirectory d)
            {
                return new
                {
                    type = "dir",
                    name = d.Name.GetName(),
                    index = d.FileUID,
                    count = d.Count,
                    //totalSize = d.TotalSize
                } as object;
            }
            else
            {
                return null as object;
            }
        }).Where(x => x is not null).ToArray();

        return new { name = dir.Name.GetName(), items };
    }

    private static LtfsDirectory? FindDirectoryByPath(LtfsDirectory root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/") return root;

        // normalize and split
        var trimmed = path.Trim('/');
        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);

        LtfsDirectory? curr = root;
        foreach (var seg in segments)
        {
            if (curr is null) return null;
            object? next = curr[seg];
            if (next is LtfsDirectory d)
            {
                curr = d;
                continue;
            }
            else
            {
                return null;
            }
        }

        return curr;
    }
}
