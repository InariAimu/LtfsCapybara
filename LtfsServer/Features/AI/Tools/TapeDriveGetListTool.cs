using System.Text.Json;

using LtfsServer.Features.TapeDrives;

namespace LtfsServer.Features.AI.Tools;

[AIToolModule(Name = "tapedrive", Description = "Tools for tape drive operations")]
public sealed class TapeDriveGetListTool
{
    private readonly ITapeDriveService _tapeDriveService;

    public TapeDriveGetListTool(ITapeDriveService tapeDriveService)
    {
        _tapeDriveService = tapeDriveService;
    }

    [AITool(Name = "tapedrive_getlist", Description = "Get the tapedrive list.")]
    public Task<string> GetListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tapeDrives = _tapeDriveService.ScanAndSync();

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            ok = true,
            count = tapeDrives.Count,
            tapeDrives,
        }));
    }
}