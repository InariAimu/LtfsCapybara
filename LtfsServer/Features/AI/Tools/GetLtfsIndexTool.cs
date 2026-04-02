using System.Text.Json;
using LtfsServer.Features.LocalIndex;

namespace LtfsServer.Features.AI.Tools;

[AIToolModule(Name = "local_tape", Description = "Tools for reading local tape library information")]
public sealed class GetLtfsIndexTool
{
    private readonly ILocalIndexQueryService _localIndexQueryService;

    public GetLtfsIndexTool(ILocalIndexQueryService localIndexQueryService)
    {
        _localIndexQueryService = localIndexQueryService;
    }

    [AITool(Name = "get_ltfs_index", Description = "Get LTFS index directory listing for a tape barcode and optional path. Logic is the same as /api/local/{barcode}/{**file}.")]
    public Task<string> ExecuteAsync(
        [AIToolParam(Description = "barcode of the tape")] string barcode,
        [AIToolParam(Description = "directory path inside LTFS, such as / or /folder/subfolder")] string path = "/",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        barcode = (barcode ?? string.Empty).Trim();
        path = (path ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(barcode))
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                ok = false,
                error = "Missing required argument: barcode"
            }));
        }

        var result = _localIndexQueryService.GetDirectory(barcode, path);
        return Task.FromResult(JsonSerializer.Serialize(new
        {
            ok = result.StatusCode == StatusCodes.Status200OK,
            statusCode = result.StatusCode,
            barcode,
            path = LocalIndexPath.NormalizePath(path),
            data = result.Payload
        }));
    }
}