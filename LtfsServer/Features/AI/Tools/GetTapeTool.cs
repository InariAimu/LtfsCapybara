using System.Text.Json;
using LtoTape;
using LtfsServer.BootStrap;
using LtfsServer.Features.LocalIndex;
using LtfsServer.Features.LocalTapes;

namespace LtfsServer.Features.AI.Tools;

[AIToolModule(Name = "local_tape", Description = "Tools for reading local tape library information")]
public sealed class GetTapeTool
{
    private readonly ILocalTapeRegistry _localTapeRegistry;
    private readonly AppData _appData;

    public GetTapeTool(ILocalTapeRegistry localTapeRegistry, AppData appData)
    {
        _localTapeRegistry = localTapeRegistry;
        _appData = appData;
    }

    [AITool(Name = "get_tape", Description = "Get the tape info by barcode in local tape library")]
    public Task<string> GetTapeAsync(
        [AIToolParam(Description = "barcode of the tape")] string barcode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        barcode = (barcode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                ok = false,
                error = "Missing required argument: barcode"
            }));
        }

        var file = _localTapeRegistry.GetFiles(barcode)
            .Where(HasCartridgeMemory)
            .OrderByDescending(f => f.Index.Ticks)
            .FirstOrDefault();

        if (file is null)
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                ok = true,
                found = false,
                barcode,
                error = "No cartridge memory files found for tape"
            }));
        }

        var cmPath = Path.Combine(_appData.Path, "local", barcode, file.Index.FileName);
        if (!File.Exists(cmPath))
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                ok = false,
                found = false,
                barcode,
                error = "Cartridge memory file not found"
            }));
        }

        var cartridgeMemory = new CartridgeMemory();
        if (cmPath.EndsWith(".cmbin", StringComparison.OrdinalIgnoreCase))
            cartridgeMemory.FromBinaryFile(cmPath);
        else
            cartridgeMemory.FromLcgCmFile(cmPath);

        var cmDto = CartridgeMemoryDto.From(cartridgeMemory);

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            ok = true,
            found = true,
            barcode,
            tape = cmDto
        }));
    }

    private static bool HasCartridgeMemory(TapeFileInfo file)
    {
        return file.Index.FileName.EndsWith(".cm", StringComparison.OrdinalIgnoreCase)
            || file.Index.FileName.EndsWith(".cmbin", StringComparison.OrdinalIgnoreCase);
    }
}
