using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Features;

namespace LtfsServer.Features.AI;

public interface IAiChatProxyService
{
    Task HandleChatCompletionAsync(HttpContext context, CancellationToken cancellationToken);
}

public sealed class AiChatProxyService : IAiChatProxyService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IAiToolCallService _toolCallService;
    private readonly IAiToolSelectionService _toolSelectionService;
    private readonly ILogger<AiChatProxyService> _logger;

    public AiChatProxyService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IAiToolCallService toolCallService,
        IAiToolSelectionService toolSelectionService,
        ILogger<AiChatProxyService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _toolCallService = toolCallService;
        _toolSelectionService = toolSelectionService;
        _logger = logger;
    }

    public async Task HandleChatCompletionAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var requestNode = await JsonNode.ParseAsync(context.Request.Body, cancellationToken: cancellationToken) as JsonObject;
        if (requestNode is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "Invalid JSON request body." }, cancellationToken: cancellationToken);
            return;
        }

        var messages = requestNode["messages"] as JsonArray;
        if (messages is null || messages.Count == 0)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "Request must include non-empty messages." }, cancellationToken: cancellationToken);
            return;
        }

        var model = requestNode["model"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(model))
        {
            model = ReadSetting("AI:model", "AI:Model") ?? "deepseek-chat";
            requestNode["model"] = model;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        context.Response.ContentType = "text/event-stream";
        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        await _toolSelectionService.PrepareRequestAsync(requestNode, model, context.Response, cancellationToken);
        requestNode["stream"] = true;

        await StreamWithToolsAsync(context.Response, requestNode, cancellationToken);
    }

    private async Task StreamWithToolsAsync(HttpResponse response, JsonObject requestNode, CancellationToken cancellationToken)
    {
        const int maxToolIterations = 6;
        for (var iteration = 0; iteration < maxToolIterations; iteration++)
        {
            var turn = await CallUpstreamAndRelayAsync(requestNode, response, cancellationToken);
            var assistantMessage = turn.AssistantMessage;
            if (assistantMessage is null)
            {
                await WriteSseDoneAsync(response, cancellationToken);
                return;
            }

            var toolCalls = assistantMessage["tool_calls"] as JsonArray;
            if (toolCalls is null || toolCalls.Count == 0)
            {
                await WriteSseDoneAsync(response, cancellationToken);
                return;
            }

            var messages = requestNode["messages"] as JsonArray;
            if (messages is null)
            {
                throw new InvalidOperationException("Request has no messages array.");
            }

            messages.Add(assistantMessage.DeepClone());

            foreach (var toolCallNode in toolCalls)
            {
                var toolCall = toolCallNode as JsonObject;
                if (toolCall is null)
                {
                    continue;
                }

                var functionNode = toolCall["function"] as JsonObject;
                var toolName = functionNode?["name"]?.GetValue<string>() ?? string.Empty;
                var argumentsJson = functionNode?["arguments"]?.GetValue<string>() ?? "{}";
                var toolCallId = toolCall["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N");

                var toolResult = await _toolCallService.ExecuteAsync(toolName, argumentsJson, cancellationToken);
                messages.Add(new JsonObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = toolCallId,
                    ["name"] = toolName,
                    ["content"] = toolResult
                });
            }
        }

        await WriteSseDataAsync(response, new
        {
            id = $"chatcmpl-{Guid.NewGuid():N}",
            @object = "chat.completion.chunk",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            model = requestNode["model"]?.GetValue<string>() ?? "deepseek-chat",
            choices = new[]
            {
                new
                {
                    index = 0,
                    delta = new { content = "\n\n[tool-loop] iteration limit reached before final response." },
                    finish_reason = (string?)null
                }
            }
        }, cancellationToken);
        await WriteSseDoneAsync(response, cancellationToken);
    }

    private async Task<UpstreamTurnResult> CallUpstreamAndRelayAsync(JsonObject payload, HttpResponse downstreamResponse, CancellationToken cancellationToken)
    {
        var baseUrl = ReadSetting("AI:base_url", "AI:BaseUrl")?.Trim();
        var apiKey = ReadSetting("AI:api_key", "AI:ApiKey")?.Trim();

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("AI config is missing. Please set AI.base_url and AI.api_key in {Data.Path}/config.json.");
        }

        var endpoint = ResolveChatCompletionsEndpoint(baseUrl);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var client = _httpClientFactory.CreateClient("AiServerProxy");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Upstream AI call failed: {status} {body}", (int)response.StatusCode, errorContent);
            throw new InvalidOperationException($"Upstream AI call failed with status {(int)response.StatusCode}: {errorContent}");
        }

        if (response.Content.Headers.ContentType?.MediaType?.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) == true)
        {
            return await ReadAndRelayStreamResponseAsync(response, downstreamResponse, cancellationToken);
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseNode = JsonNode.Parse(responseContent) as JsonObject;
        if (responseNode is null)
        {
            throw new InvalidOperationException("Upstream AI response is not a valid JSON object.");
        }

        var model = responseNode["model"]?.GetValue<string>() ?? payload["model"]?.GetValue<string>() ?? "deepseek-chat";
        var assistantMessage = responseNode["choices"]?[0]?["message"] as JsonObject;
        var content = ReadContent(assistantMessage?["content"]);

        if (!string.IsNullOrEmpty(content))
        {
            await WriteSseDataAsync(downstreamResponse, new
            {
                id = $"chatcmpl-{Guid.NewGuid():N}",
                @object = "chat.completion.chunk",
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                model,
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new { role = "assistant", content },
                        finish_reason = (string?)null
                    }
                }
            }, cancellationToken);
        }

        return new UpstreamTurnResult(model, assistantMessage?.DeepClone() as JsonObject);
    }

    private static async Task<UpstreamTurnResult> ReadAndRelayStreamResponseAsync(
        HttpResponseMessage upstreamResponse,
        HttpResponse downstreamResponse,
        CancellationToken cancellationToken)
    {
        await using var stream = await upstreamResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var assistantContent = new StringBuilder();
        var role = "assistant";
        var model = "deepseek-chat";
        var toolCalls = new Dictionary<int, JsonObject>();
        var eventDataLines = new List<string>();

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrEmpty(line))
            {
                if (eventDataLines.Count == 0)
                {
                    continue;
                }

                var payload = string.Join("\n", eventDataLines);
                eventDataLines.Clear();

                if (string.IsNullOrWhiteSpace(payload) || payload == "[DONE]")
                {
                    continue;
                }

                //Console.WriteLine($"Received chunk payload: {payload}");
                await WriteSseRawDataAsync(downstreamResponse, payload, cancellationToken);

                var chunk = JsonNode.Parse(payload) as JsonObject;
                if (chunk is null)
                {
                    continue;
                }

                model = chunk["model"]?.GetValue<string>() ?? model;

                var delta = chunk["choices"]?[0]?["delta"] as JsonObject;
                if (delta is null)
                {
                    continue;
                }

                role = delta["role"]?.GetValue<string>() ?? role;
                var contentDelta = delta["content"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(contentDelta))
                {
                    assistantContent.Append(contentDelta);
                }

                var toolCallsDelta = delta["tool_calls"] as JsonArray;
                if (toolCallsDelta is null)
                {
                    continue;
                }

                foreach (var toolNode in toolCallsDelta)
                {
                    var item = toolNode as JsonObject;
                    if (item is null)
                    {
                        continue;
                    }

                    var index = item["index"]?.GetValue<int>() ?? 0;
                    if (!toolCalls.TryGetValue(index, out var merged))
                    {
                        merged = new JsonObject
                        {
                            ["id"] = item["id"]?.GetValue<string>() ?? string.Empty,
                            ["type"] = item["type"]?.GetValue<string>() ?? "function",
                            ["function"] = new JsonObject
                            {
                                ["name"] = string.Empty,
                                ["arguments"] = string.Empty
                            }
                        };
                        toolCalls[index] = merged;
                    }

                    var functionNode = merged["function"] as JsonObject;
                    var functionDelta = item["function"] as JsonObject;
                    if (functionNode is null || functionDelta is null)
                    {
                        continue;
                    }

                    var idDelta = item["id"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(idDelta))
                    {
                        merged["id"] = idDelta;
                    }

                    var nameDelta = functionDelta["name"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(nameDelta))
                    {
                        var currentName = functionNode["name"]?.GetValue<string>() ?? string.Empty;
                        functionNode["name"] = currentName + nameDelta;
                    }

                    var argsDelta = functionDelta["arguments"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(argsDelta))
                    {
                        var currentArgs = functionNode["arguments"]?.GetValue<string>() ?? string.Empty;
                        functionNode["arguments"] = currentArgs + argsDelta;
                    }
                }
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var linePayload = line.Substring(5).TrimStart();
            eventDataLines.Add(linePayload);
        }

        if (eventDataLines.Count > 0)
        {
            var payload = string.Join("\n", eventDataLines);
            if (!string.IsNullOrWhiteSpace(payload) && payload != "[DONE]")
            {
                await WriteSseRawDataAsync(downstreamResponse, payload, cancellationToken);
            }
        }

        var toolCallsArray = new JsonArray();
        foreach (var kvp in toolCalls.OrderBy(k => k.Key))
        {
            toolCallsArray.Add(kvp.Value);
        }

        var assistantMessage = new JsonObject
        {
            ["role"] = role,
            ["content"] = assistantContent.ToString()
        };

        if (toolCallsArray.Count > 0)
        {
            assistantMessage["tool_calls"] = toolCallsArray;
        }

        return new UpstreamTurnResult(model, assistantMessage);
    }

    private static async Task WriteSseRawDataAsync(HttpResponse response, string payload, CancellationToken cancellationToken)
    {
        await response.WriteAsync($"data: {payload}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static async Task WriteSseDoneAsync(HttpResponse response, CancellationToken cancellationToken)
    {
        await response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static async Task WriteSseDataAsync(HttpResponse response, object data, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(data);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static string ReadContent(JsonNode? contentNode)
    {
        if (contentNode is null)
        {
            return string.Empty;
        }

        if (contentNode is JsonValue)
        {
            return contentNode.GetValue<string>();
        }

        return contentNode.ToJsonString();
    }

    private string? ReadSetting(string primaryKey, string fallbackKey)
    {
        return _configuration[primaryKey] ?? _configuration[fallbackKey];
    }

    private static string ResolveChatCompletionsEndpoint(string baseUrl)
    {
        var normalized = baseUrl.TrimEnd('/');
        if (normalized.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        return $"{normalized}/chat/completions";
    }

    private sealed record UpstreamTurnResult(string Model, JsonObject? AssistantMessage);
}
