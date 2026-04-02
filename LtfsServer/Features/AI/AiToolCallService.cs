using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using LtfsServer.Features.AI.Tools;

namespace LtfsServer.Features.AI;

public interface IAiToolCallService
{
    JsonArray GetToolDefinitions();
    JsonArray GetToolDefinitionsByName(IEnumerable<string> toolNames);
    IReadOnlyList<string> GetAllToolNames();
    JsonObject GetAllAITools();
    Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken);
}

public sealed class AiToolCallService : IAiToolCallService
{
    private readonly IReadOnlyDictionary<string, RegisteredTool> _tools;
    private readonly IReadOnlyList<RegisteredModule> _modules;

    public AiToolCallService(IServiceProvider serviceProvider)
    {
        var tools = DiscoverTools(serviceProvider).ToList();
        var duplicate = tools
            .GroupBy(t => t.Name, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Duplicate AI tool registration: {duplicate.Key}");

        _tools = tools.ToDictionary(t => t.Name, t => t, StringComparer.Ordinal);
        _modules = tools
            .GroupBy(t => new ModuleKey(t.ModuleName, t.ModuleDescription))
            .OrderBy(g => g.Key.Name, StringComparer.Ordinal)
            .Select(g => new RegisteredModule(
                Name: g.Key.Name,
                Description: g.Key.Description,
                Tools: g
                    .OrderBy(t => t.Name, StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();
    }

    public JsonArray GetToolDefinitions()
    {
        var result = new JsonArray();
        foreach (var tool in _tools.Values.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            result.Add(tool.Definition.DeepClone());
        }

        return result;
    }

    public JsonArray GetToolDefinitionsByName(IEnumerable<string> toolNames)
    {
        var result = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var toolName in toolNames)
        {
            if (string.IsNullOrWhiteSpace(toolName) || !seen.Add(toolName))
            {
                continue;
            }

            if (_tools.TryGetValue(toolName, out var tool))
            {
                result.Add(tool.Definition.DeepClone());
            }
        }

        return result;
    }

    public IReadOnlyList<string> GetAllToolNames()
    {
        return _tools.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    public JsonObject GetAllAITools()
    {
        var modules = new JsonArray();
        foreach (var module in _modules)
        {
            var methods = new JsonArray();
            foreach (var tool in module.Tools)
            {
                methods.Add(new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["definition"] = tool.Definition.DeepClone()
                });
            }

            modules.Add(new JsonObject
            {
                ["name"] = module.Name,
                ["description"] = module.Description,
                ["methods"] = methods
            });
        }

        return new JsonObject
        {
            ["moduleCount"] = _modules.Count,
            ["toolCount"] = _tools.Count,
            ["modules"] = modules
        };
    }

    public Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(toolName))
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                ok = false,
                error = "Missing tool name"
            }));
        }

        if (!_tools.TryGetValue(toolName, out var tool))
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                ok = false,
                error = $"Unsupported tool: {toolName}"
            }));
        }

        try
        {
            return tool.InvokeAsync(argumentsJson, cancellationToken);
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

    private static IEnumerable<RegisteredTool> DiscoverTools(IServiceProvider serviceProvider)
    {
        var assembly = typeof(AiToolCallService).Assembly;
        var moduleTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Select(t => new
            {
                Type = t,
                Module = t.GetCustomAttribute<AIToolModuleAttribute>()
            })
            .Where(x => x.Module is not null)
            .Select(x => (x.Type, Module: x.Module!));

        foreach (var (moduleType, moduleAttr) in moduleTypes)
        {
            var moduleInstance = serviceProvider.GetRequiredService(moduleType);
            var methods = moduleType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Select(m => new
                {
                    Method = m,
                    Tool = m.GetCustomAttribute<AIToolAttribute>()
                })
                .Where(x => x.Tool is not null)
                .Select(x => (x.Method, Tool: x.Tool!));

            foreach (var (method, toolAttr) in methods)
            {
                yield return CreateRegisteredTool(moduleAttr, moduleInstance, method, toolAttr);
            }
        }
    }

    private static RegisteredTool CreateRegisteredTool(
        AIToolModuleAttribute moduleAttr,
        object moduleInstance,
        MethodInfo method,
        AIToolAttribute toolAttr)
    {
        var parameters = method.GetParameters();
        var paramBindings = new List<ToolParamBinding>();
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var paramAttr = parameter.GetCustomAttribute<AIToolParamAttribute>();
            if (paramAttr is null)
            {
                if (parameter.ParameterType == typeof(CancellationToken))
                    continue;

                throw new InvalidOperationException($"Method {method.DeclaringType?.Name}.{method.Name} has unsupported parameter '{parameter.Name}'. Use [AIToolParam] string or CancellationToken.");
            }

            if (parameter.ParameterType != typeof(string))
                throw new InvalidOperationException($"Method {method.DeclaringType?.Name}.{method.Name} parameter '{parameter.Name}' must be string for [AIToolParam].");

            if (string.IsNullOrWhiteSpace(parameter.Name))
                throw new InvalidOperationException($"Method {method.DeclaringType?.Name}.{method.Name} has unnamed parameter.");

            paramBindings.Add(new ToolParamBinding(
                Index: i,
                Name: parameter.Name,
                Description: paramAttr.Description,
                Required: !parameter.IsOptional));
        }

        var definition = BuildDefinition(toolAttr.Name, $"[{moduleAttr.Name}] {toolAttr.Description}", paramBindings);
        return new RegisteredTool(
            Name: toolAttr.Name,
            Description: toolAttr.Description,
            ModuleName: moduleAttr.Name,
            ModuleDescription: moduleAttr.Description,
            Definition: definition,
            InvokeAsync: (argumentsJson, cancellationToken) =>
            InvokeToolAsync(moduleInstance, method, paramBindings, argumentsJson, cancellationToken));
    }

    private static JsonObject BuildDefinition(string toolName, string toolDescription, IReadOnlyList<ToolParamBinding> parameters)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var param in parameters)
        {
            properties[param.Name] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = param.Description
            };

            if (param.Required)
                required.Add(param.Name);
        }

        return new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = toolName,
                ["description"] = toolDescription,
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = required
                }
            }
        };
    }

    private static async Task<string> InvokeToolAsync(
        object moduleInstance,
        MethodInfo method,
        IReadOnlyList<ToolParamBinding> paramBindings,
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        JsonObject arguments;
        try
        {
            arguments = string.IsNullOrWhiteSpace(argumentsJson)
                ? new JsonObject()
                : (JsonNode.Parse(argumentsJson) as JsonObject ?? new JsonObject());
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new
            {
                ok = false,
                error = $"Invalid tool arguments JSON: {ex.Message}"
            });
        }

        var parameters = method.GetParameters();
        var invokeArgs = new object?[parameters.Length];

        foreach (var binding in paramBindings)
        {
            var value = GetCaseInsensitiveString(arguments, binding.Name);
            if (string.IsNullOrWhiteSpace(value) && binding.Required)
            {
                return JsonSerializer.Serialize(new
                {
                    ok = false,
                    error = $"Missing required argument: {binding.Name}"
                });
            }

            invokeArgs[binding.Index] = value ?? string.Empty;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(CancellationToken))
                invokeArgs[i] = cancellationToken;
        }

        try
        {
            var returnValue = method.Invoke(moduleInstance, invokeArgs);
            return await NormalizeReturnValueAsync(returnValue).ConfigureAwait(false);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return JsonSerializer.Serialize(new
            {
                ok = false,
                error = ex.InnerException.Message
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                ok = false,
                error = ex.Message
            });
        }
    }

    private static string? GetCaseInsensitiveString(JsonObject json, string key)
    {
        if (json.TryGetPropertyValue(key, out var exactValue))
            return exactValue?.GetValue<string>();

        foreach (var kvp in json)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value?.GetValue<string>();
        }

        return null;
    }

    private static async Task<string> NormalizeReturnValueAsync(object? returnValue)
    {
        if (returnValue is null)
            return JsonSerializer.Serialize(new { ok = true });

        if (returnValue is string plainString)
            return plainString;

        if (returnValue is Task task)
        {
            await task.ConfigureAwait(false);
            var taskType = task.GetType();
            if (taskType.IsGenericType)
            {
                var resultProp = taskType.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
                var taskResult = resultProp?.GetValue(task);
                if (taskResult is string stringResult)
                    return stringResult;

                return JsonSerializer.Serialize(taskResult);
            }

            return JsonSerializer.Serialize(new { ok = true });
        }

        return JsonSerializer.Serialize(returnValue);
    }

    private sealed record RegisteredTool(
        string Name,
        string Description,
        string ModuleName,
        string ModuleDescription,
        JsonObject Definition,
        Func<string, CancellationToken, Task<string>> InvokeAsync);

    private sealed record RegisteredModule(
        string Name,
        string Description,
        IReadOnlyList<RegisteredTool> Tools);

    private sealed record ModuleKey(string Name, string Description);

    private sealed record ToolParamBinding(
        int Index,
        string Name,
        string Description,
        bool Required);
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Instance = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
