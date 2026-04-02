using System.Text.Json;
using LtoTape;
using LtfsServer.BootStrap;
using LtfsServer.Features.LocalIndex;
using LtfsServer.Features.LocalTapes;

namespace LtfsServer.Features.AI.Tools;

[AIToolModule(Name = "tapedrive", Description = "Tools for tape drive operations")]
public sealed class TapeDriveReadCMTool
{
    private readonly ILocalTapeRegistry _localTapeRegistry;
    private readonly AppData _appData;

    public TapeDriveReadCMTool(ILocalTapeRegistry localTapeRegistry, AppData appData)
    {
        _localTapeRegistry = localTapeRegistry;
        _appData = appData;
    }

    [AITool(Name = "tapedrive_read_cm", Description = "Read the barcode and cartridge memory info of the tape cartridge in tapedrive. result: barcode and cartridge memory info of the tape. This operation must be called after load or thread operation, and before eject or unthread operation.")]
    public Task<string> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Task.Delay(5000).Wait(cancellationToken);

        var barcode = "CAT001L5";
        
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
            barcode = barcode,
            cartridgeMemory = cmDto
        }));
    }

    private static bool HasCartridgeMemory(TapeFileInfo file)
    {
        return file.Index.FileName.EndsWith(".cm", StringComparison.OrdinalIgnoreCase)
            || file.Index.FileName.EndsWith(".cmbin", StringComparison.OrdinalIgnoreCase);
    }
}
