using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;

namespace LtfsServer.Features.AI;

public interface IAiToolSelectionService
{
    Task<AiToolSelectionResult> PrepareRequestAsync(
        JsonObject requestNode,
        string model,
        HttpResponse? downstreamResponse,
        CancellationToken cancellationToken);
}

public sealed class AiToolSelectionService : IAiToolSelectionService
{
    private const string ToolExposurePromptPrefix = "Tool exposure for this turn:";
    private const string ToolSelectionModel = "deepseek-v4-flash";
    private const string SelectorSystemPrompt = """
You are an AI tool planner for an LTFS tape management system.
Your job is to choose which tools should be exposed to the assistant for the next assistant turn.

Rules:
1. Only choose tools that are directly relevant to the user's latest request and nearby context.
2. Prefer the smallest useful tool set.
3. If the user only wants explanation, translation, summarization, or general conversation, choose no tools.
4. If a request clearly needs live tape, local tape, or LTFS index data, choose the matching tools.
5. Return strict JSON only. Do not use markdown. Do not add commentary outside JSON.

Return schema:
{
  "requiresTools": true,
  "selectedTools": ["tool_name_1", "tool_name_2"],
  "reason": "short reason",
  "assistantGuidance": "short internal guidance for the chat model"
}
""";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAiProviderConfigService _aiProviderConfigService;
    private readonly IAiToolCallService _toolCallService;
    private readonly ILogger<AiToolSelectionService> _logger;

    public AiToolSelectionService(
        IHttpClientFactory httpClientFactory,
        IAiProviderConfigService aiProviderConfigService,
        IAiToolCallService toolCallService,
        ILogger<AiToolSelectionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _aiProviderConfigService = aiProviderConfigService;
        _toolCallService = toolCallService;
        _logger = logger;
    }

    public async Task<AiToolSelectionResult> PrepareRequestAsync(
        JsonObject requestNode,
        string model,
        HttpResponse? downstreamResponse,
        CancellationToken cancellationToken)
    {
        var requestedToolNames = ReadExistingToolNames(requestNode);
        if (requestedToolNames.Count > 0)
        {
            var requestedDefs = _toolCallService.GetToolDefinitionsByName(requestedToolNames);
            ReplaceTools(requestNode, requestedDefs);
            return new AiToolSelectionResult(
                RequiresTools: requestedDefs.Count > 0,
                SelectedToolNames: requestedToolNames.ToArray(),
                ToolDefinitions: requestedDefs,
                Reason: "Used tools explicitly provided by the request.",
                AssistantGuidance: "Use only the explicitly provided tools when needed.");
        }

        var selectorResult = await SelectToolsAsync(requestNode, model, downstreamResponse, cancellationToken);
        var toolDefinitions = selectorResult.RequiresTools
            ? _toolCallService.GetToolDefinitionsByName(selectorResult.SelectedToolNames)
            : new JsonArray();

        if (selectorResult.RequiresTools && toolDefinitions.Count == 0)
        {
            toolDefinitions = _toolCallService.GetToolDefinitions();
            selectorResult = selectorResult with
            {
                SelectedToolNames = _toolCallService.GetAllToolNames().ToArray(),
                Reason = "Tool selector returned no valid tool names, so all tools were exposed as a fallback.",
                AssistantGuidance = "Use tools only when needed. The selection step failed to map named tools, so all tools are available."
            };
        }

        ReplaceTools(requestNode, toolDefinitions);
        AppendSelectionSystemMessage(requestNode, selectorResult);

        return new AiToolSelectionResult(
            selectorResult.RequiresTools,
            selectorResult.SelectedToolNames,
            toolDefinitions,
            selectorResult.Reason,
            selectorResult.AssistantGuidance);
    }

    private async Task<AiToolSelectionResult> SelectToolsAsync(
        JsonObject requestNode,
        string model,
        HttpResponse? downstreamResponse,
        CancellationToken cancellationToken)
    {
        var messages = requestNode["messages"] as JsonArray ?? new JsonArray();
        var selectorRequest = new JsonObject
        {
            ["model"] = ToolSelectionModel,
            ["stream"] = true,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = SelectorSystemPrompt
                },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = BuildSelectionUserPrompt(messages, _toolCallService.GetAllAITools())
                }
            }
        };

        try
        {
            var rawContent = await SendStreamingSelectionRequestAsync(selectorRequest, downstreamResponse, cancellationToken);
            var parsed = ParseSelectionResult(rawContent);
            if (parsed is not null)
            {
                return parsed;
            }

            _logger.LogWarning("AI tool selector returned unparsable content: {content}", rawContent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI tool selection failed. Falling back to all tools.");
        }

        return new AiToolSelectionResult(
            RequiresTools: true,
            SelectedToolNames: _toolCallService.GetAllToolNames().ToArray(),
            Reason: "Tool selection failed, so all tools were exposed as a fallback.",
            AssistantGuidance: "Use tools only when necessary. All tools are available because the selection step failed.",
            ToolDefinitions: null);
    }

    private async Task<string> SendStreamingSelectionRequestAsync(
        JsonObject payload,
        HttpResponse? downstreamResponse,
        CancellationToken cancellationToken)
    {
        var resolvedProvider = _aiProviderConfigService.ResolveForModel(ToolSelectionModel);
        payload["model"] = resolvedProvider.Model;

        var endpoint = ResolveChatCompletionsEndpoint(resolvedProvider.BaseUrl);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resolvedProvider.ApiKey);

        var client = _httpClientFactory.CreateClient("AiServerProxy");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Upstream AI tool selection failed with status {(int)response.StatusCode}: {responseContent}");
        }

        if (response.Content.Headers.ContentType?.MediaType?.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) == true)
        {
            return await ReadStreamingSelectionResponseAsync(response, downstreamResponse, cancellationToken);
        }

        var responseContentPlain = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseNode = JsonNode.Parse(responseContentPlain) as JsonObject
            ?? throw new InvalidOperationException("Upstream AI tool selection response is not a valid JSON object.");
        var rawContent = ExtractAssistantContent(responseNode);
        if (downstreamResponse is not null && !string.IsNullOrWhiteSpace(rawContent))
        {
            await WriteStageChunkAsync(
                downstreamResponse,
                CreateStageChunk(model: payload["model"]?.GetValue<string>() ?? "deepseek-chat", content: rawContent),
                cancellationToken);
        }

        return rawContent;
    }

    private static async Task<string> ReadStreamingSelectionResponseAsync(
        HttpResponseMessage upstreamResponse,
        HttpResponse? downstreamResponse,
        CancellationToken cancellationToken)
    {
        await using var stream = await upstreamResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var contentBuilder = new StringBuilder();
        var reasoningBuilder = new StringBuilder();
        var eventDataLines = new List<string>();
        var model = "deepseek-chat";

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

                var reasoning = delta["reasoning_content"]?.GetValue<string>()
                    ?? delta["reasoning"]?.GetValue<string>()
                    ?? string.Empty;
                if (!string.IsNullOrEmpty(reasoning))
                {
                    reasoningBuilder.Append(reasoning);
                    if (downstreamResponse is not null)
                    {
                        await WriteStageChunkAsync(
                            downstreamResponse,
                            CreateStageChunk(model, reasoning: reasoning),
                            cancellationToken);
                    }
                }

                var content = ExtractDeltaText(delta);
                if (!string.IsNullOrEmpty(content))
                {
                    contentBuilder.Append(content);
                    if (downstreamResponse is not null)
                    {
                        await WriteStageChunkAsync(
                            downstreamResponse,
                            CreateStageChunk(model, content: content),
                            cancellationToken);
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

        return contentBuilder.Length > 0 ? contentBuilder.ToString() : reasoningBuilder.ToString();
    }

    private static string BuildSelectionUserPrompt(JsonArray messages, JsonObject allTools)
    {
        var compactMessages = new JsonArray();
        foreach (var messageNode in messages.TakeLast(8))
        {
            var message = messageNode as JsonObject;
            if (message is null)
            {
                continue;
            }

            compactMessages.Add(new JsonObject
            {
                ["role"] = message["role"]?.GetValue<string>() ?? string.Empty,
                ["content"] = ReadContent(message["content"])
            });
        }

        return $$"""
Recent chat messages:
{{compactMessages.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}}

Available AI modules and tools:
{{allTools.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}}

Choose the smallest set of tools needed for the assistant's next response to the user.
If no tool is needed, return requiresTools=false and an empty selectedTools array.
""";
    }

    private static AiToolSelectionResult? ParseSelectionResult(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        JsonObject? node = null;
        try
        {
            node = JsonNode.Parse(rawContent) as JsonObject;
        }
        catch (JsonException)
        {
            var start = rawContent.IndexOf('{');
            var end = rawContent.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                var json = rawContent[start..(end + 1)];
                node = JsonNode.Parse(json) as JsonObject;
            }
        }

        if (node is null)
        {
            return null;
        }

        var requiresTools = node["requiresTools"]?.GetValue<bool>() ?? false;
        var reason = node["reason"]?.GetValue<string>() ?? string.Empty;
        var assistantGuidance = node["assistantGuidance"]?.GetValue<string>() ?? string.Empty;
        var selectedTools = node["selectedTools"] as JsonArray;

        var names = new List<string>();
        if (selectedTools is not null)
        {
            foreach (var item in selectedTools)
            {
                var name = item?.GetValue<string>()?.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }
        }

        if (!requiresTools)
        {
            names.Clear();
        }

        return new AiToolSelectionResult(
            RequiresTools: requiresTools,
            SelectedToolNames: names.Distinct(StringComparer.Ordinal).ToArray(),
            Reason: reason,
            AssistantGuidance: assistantGuidance,
            ToolDefinitions: null);
    }

    private static string ExtractAssistantContent(JsonObject responseNode)
    {
        var contentNode = responseNode["choices"]?[0]?["message"]?["content"];
        return ReadContent(contentNode);
    }

    private static string ExtractDeltaText(JsonObject delta)
    {
        var contentNode = delta["content"];
        return ReadContent(contentNode);
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

        if (contentNode is JsonArray contentArray)
        {
            var parts = new List<string>();
            foreach (var item in contentArray)
            {
                var text = item?["text"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    parts.Add(text);
                }
            }

            return string.Join("\n", parts);
        }

        return contentNode.ToJsonString();
    }

    private static HashSet<string> ReadExistingToolNames(JsonObject requestNode)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var tools = requestNode["tools"] as JsonArray;
        if (tools is null)
        {
            return result;
        }

        foreach (var item in tools)
        {
            var name = item?["function"]?["name"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(name);
            }
        }

        return result;
    }

    private static void ReplaceTools(JsonObject requestNode, JsonArray toolDefinitions)
    {
        if (toolDefinitions.Count == 0)
        {
            requestNode.Remove("tools");
            return;
        }

        requestNode["tools"] = toolDefinitions;
    }

    private static void AppendSelectionSystemMessage(JsonObject requestNode, AiToolSelectionResult selection)
    {
        var messages = requestNode["messages"] as JsonArray;
        if (messages is null)
        {
            return;
        }

        RemovePreviousSelectionSystemMessages(messages);

        var selectedList = selection.SelectedToolNames.Length == 0
            ? "none"
            : string.Join(", ", selection.SelectedToolNames);

        var insertIndex = 0;
        while (insertIndex < messages.Count
               && string.Equals(messages[insertIndex]?["role"]?.GetValue<string>(), "system", StringComparison.Ordinal))
        {
            insertIndex++;
        }

        messages.Insert(insertIndex, new JsonObject
        {
            ["role"] = "system",
            ["content"] = $$"""
{{ToolExposurePromptPrefix}}
- requiresTools: {{selection.RequiresTools}}
- selectedTools: {{selectedList}}
- reason: {{selection.Reason}}
- guidance: {{selection.AssistantGuidance}}

Only call a tool when it is necessary to answer the user correctly. If no tool is needed, answer directly.
"""
        });
    }

    private static void RemovePreviousSelectionSystemMessages(JsonArray messages)
    {
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            var message = messages[i] as JsonObject;
            if (message is null)
            {
                continue;
            }

            var role = message["role"]?.GetValue<string>();
            var content = ReadContent(message["content"]);
            if (string.Equals(role, "system", StringComparison.Ordinal)
                && content.StartsWith(ToolExposurePromptPrefix, StringComparison.Ordinal))
            {
                messages.RemoveAt(i);
            }
        }
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

    private static JsonObject CreateStageChunk(string model, string? content = null, string? reasoning = null)
    {
        var delta = new JsonObject();
        if (!string.IsNullOrEmpty(reasoning))
        {
            delta["reasoning"] = reasoning;
        }

        if (!string.IsNullOrEmpty(content))
        {
            delta["content"] = content;
        }

        return new JsonObject
        {
            ["id"] = "toolsel-stage",
            ["object"] = "chat.completion.chunk",
            ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["model"] = model,
            ["ltfs_stage"] = "tool_selection",
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["index"] = 0,
                    ["delta"] = delta,
                    ["finish_reason"] = null
                }
            }
        };
    }

    private static async Task WriteStageChunkAsync(
        HttpResponse response,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        await response.WriteAsync($"data: {payload.ToJsonString()}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}

public sealed record AiToolSelectionResult(
    bool RequiresTools,
    string[] SelectedToolNames,
    JsonArray? ToolDefinitions,
    string Reason,
    string AssistantGuidance);
