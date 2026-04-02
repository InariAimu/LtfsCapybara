using System.Text.Json;

namespace LtfsServer.Features.AI.Tools;

[AIToolModule(Name = "tapedrive", Description = "Tools for tape drive operations")]
public sealed class TapeDriveOperationTool
{
    [AITool(Name = "tapedrive_operation", Description = "Operate tapedrive, and get operation result. the operation type is one of 4: [load | eject | thread | unthread] (load: load tape cartridge only. thread: load cartridge if unload, then load actual tape. unthread: unload tape, but keep cartridge. eject: unload tape if loaded, then eject cartridge.)")]
    public Task<string> ExecuteAsync(
        [AIToolParam(Description = "tapedrive operation type.")] string operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        operation = operation.Trim();

        if (operation != "load" && operation != "eject" && operation != "thread" && operation != "unthread")
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                ok = false,
                error = "Unsupported operation"
            }));
        }

        Task.Delay(5000).Wait(cancellationToken);

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            ok = true,
            operation,
            message = "Operation success",
        }));
    }
}
