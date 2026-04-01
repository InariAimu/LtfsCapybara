using System.Text.Json;
using System.Text.Json.Nodes;
using LtoTape;
using LtfsServer.BootStrap;
using LtfsServer.Features.LocalIndex;
using LtfsServer.Features.LocalTapes;

namespace LtfsServer.Features.AI;

public interface IAiToolCallService
{
    string GetToolName();
    JsonObject GetToolDefinition();
    Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken);
}

public sealed class AiToolCallService : IAiToolCallService
{
    private readonly ILocalTapeRegistry _localTapeRegistry;
    private readonly AppData _appData;

    public AiToolCallService(ILocalTapeRegistry localTapeRegistry, AppData appData)
    {
        _localTapeRegistry = localTapeRegistry;
        _appData = appData;
    }

    public string GetToolName() => "get_tape";

    public JsonObject GetToolDefinition()
    {
        return new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = "get_tape",
                ["description"] = "Get the tape info by barcode in local tape library",
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["barcode"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "barcode of the tape"
                        }
                    },
                    ["required"] = new JsonArray("barcode")
                }
            }
        };
    }

    public Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(toolName, GetToolName(), StringComparison.Ordinal))
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                ok = false,
                error = $"Unsupported tool: {toolName}"
            }));
        }

        try
        {
            var args = JsonSerializer.Deserialize<GetTapeArgs>(argumentsJson, JsonOptions.Instance) ?? new GetTapeArgs();
            var barcode = (args.Barcode ?? string.Empty).Trim();
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
        catch (Exception ex)
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                ok = false,
                error = ex.Message
            }));
        }
    }

    private sealed class GetTapeArgs
    {
        public string? Barcode { get; set; }
    }

    private static bool HasCartridgeMemory(TapeFileInfo file)
    {
        return file.Index.FileName.EndsWith(".cm", StringComparison.OrdinalIgnoreCase)
            || file.Index.FileName.EndsWith(".cmbin", StringComparison.OrdinalIgnoreCase);
    }
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Instance = new()
    {
        PropertyNameCaseInsensitive = true
    };
}