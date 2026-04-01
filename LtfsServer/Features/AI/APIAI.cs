namespace LtfsServer.Features.AI;

public static class APIAI
{
    public static void MapAiApi(this WebApplication app)
    {
        app.MapPost("api/ai/chat/completions", async (
            HttpContext context,
            IAiChatProxyService aiChatProxyService,
            CancellationToken cancellationToken) =>
        {
            await aiChatProxyService.HandleChatCompletionAsync(context, cancellationToken);
        });

        // Alias endpoint used by the UI resend flow.
        app.MapPost("api/ai/resend", async (
            HttpContext context,
            IAiChatProxyService aiChatProxyService,
            CancellationToken cancellationToken) =>
        {
            await aiChatProxyService.HandleChatCompletionAsync(context, cancellationToken);
        });
    }
}