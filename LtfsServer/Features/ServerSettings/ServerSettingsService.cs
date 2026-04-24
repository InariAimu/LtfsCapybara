using System.Text.Json;
using System.Text.Json.Nodes;

namespace LtfsServer.Features.ServerSettings;

public interface IServerSettingsService
{
    ServerSettingsDto Get();
    ServerSettingsDto Save(ServerSettingsUpdateRequest request);
}

public sealed class ServerSettingsService : IServerSettingsService
{
    private const int MinOptionId = 1;
    private const int MaxOptionId = 2;
    private const string DefaultAiModel = "deepseek-chat";

    private readonly object _syncRoot = new();
    private readonly string _appSettingsPath;

    public ServerSettingsService(IHostEnvironment hostEnvironment)
    {
        _appSettingsPath = Path.Combine(hostEnvironment.ContentRootPath, "appsettings.json");
    }

    public ServerSettingsDto Get()
    {
        lock (_syncRoot)
        {
            var root = ReadSettingsRoot();
            var dataPath = ResolveDataPath((root["Data"] as JsonObject)?["Path"]?.GetValue<string>());
            var runtimeRoot = ReadRuntimeSettingsRoot(dataPath);
            return ExtractSettings(root, runtimeRoot);
        }
    }

    public ServerSettingsDto Save(ServerSettingsUpdateRequest request)
    {
        var dataPartitionId = NormalizeOptionId(request.IndexOnDataPartitionId);
        var indexPartitionId = NormalizeOptionId(request.IndexOnIndexPartitionId);
        var dataPath = request.DataPath?.Trim() ?? string.Empty;
        var aiModel = NormalizeAiModel(request.AiModel);
        var showAspNetCoreLogs = request.ShowAspNetCoreLogs;

        lock (_syncRoot)
        {
            var root = ReadSettingsRoot();

            var serverSettingsNode = root["ServerSettings"] as JsonObject;
            if (serverSettingsNode is null)
            {
                serverSettingsNode = new JsonObject();
                root["ServerSettings"] = serverSettingsNode;
            }

            serverSettingsNode["IndexOnDataPartitionId"] = dataPartitionId;
            serverSettingsNode["IndexOnIndexPartitionId"] = indexPartitionId;
            serverSettingsNode["ShowAspNetCoreLogs"] = showAspNetCoreLogs;

            var dataNode = root["Data"] as JsonObject;
            if (dataNode is null)
            {
                dataNode = new JsonObject();
                root["Data"] = dataNode;
            }

            // Reuse existing Data:Path for runtime behavior and expose it in settings UI.
            dataNode["Path"] = dataPath;

            var loggingNode = root["Logging"] as JsonObject;
            if (loggingNode is null)
            {
                loggingNode = new JsonObject();
                root["Logging"] = loggingNode;
            }

            var logLevelNode = loggingNode["LogLevel"] as JsonObject;
            if (logLevelNode is null)
            {
                logLevelNode = new JsonObject();
                loggingNode["LogLevel"] = logLevelNode;
            }

            logLevelNode["Microsoft.AspNetCore"] = showAspNetCoreLogs ? "Information" : "None";

            WriteSettingsRoot(root);

            var runtimeRoot = ReadRuntimeSettingsRoot(ResolveDataPath(dataPath));
            WritePreferredAiModel(runtimeRoot, aiModel);
            WriteRuntimeSettingsRoot(ResolveDataPath(dataPath), runtimeRoot);

            return ExtractSettings(root, runtimeRoot);
        }
    }

    private JsonObject ReadSettingsRoot()
    {
        if (!File.Exists(_appSettingsPath))
        {
            return new JsonObject();
        }

        try
        {
            var json = File.ReadAllText(_appSettingsPath);
            var node = JsonNode.Parse(json) as JsonObject;
            return node ?? new JsonObject();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to read '{_appSettingsPath}'.", ex);
        }
    }

    private void WriteSettingsRoot(JsonObject root)
    {
        try
        {
            var content = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_appSettingsPath, content);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to write '{_appSettingsPath}'.", ex);
        }
    }

    private static JsonObject ReadRuntimeSettingsRoot(string dataPath)
    {
        var runtimeConfigPath = GetRuntimeConfigPath(dataPath);
        if (!File.Exists(runtimeConfigPath))
        {
            return new JsonObject();
        }

        try
        {
            var json = File.ReadAllText(runtimeConfigPath);
            var node = JsonNode.Parse(json) as JsonObject;
            return node ?? new JsonObject();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to read '{runtimeConfigPath}'.", ex);
        }
    }

    private static void WriteRuntimeSettingsRoot(string dataPath, JsonObject root)
    {
        var runtimeConfigPath = GetRuntimeConfigPath(dataPath);

        try
        {
            Directory.CreateDirectory(dataPath);
            var content = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(runtimeConfigPath, content);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to write '{runtimeConfigPath}'.", ex);
        }
    }

    private static ServerSettingsDto ExtractSettings(JsonObject root, JsonObject runtimeRoot)
    {
        var serverSettingsNode = root["ServerSettings"] as JsonObject;
        var dataNode = root["Data"] as JsonObject;
        var runtimeAiNode = runtimeRoot["AI"] as JsonObject;
        var appSettingsAiNode = root["AI"] as JsonObject;
        var aiModels = ReadAvailableAiModels(runtimeRoot, root);

        return new ServerSettingsDto
        {
            IndexOnDataPartitionId = NormalizeOptionId(serverSettingsNode?["IndexOnDataPartitionId"]?.GetValue<int?>()),
            IndexOnIndexPartitionId = NormalizeOptionId(serverSettingsNode?["IndexOnIndexPartitionId"]?.GetValue<int?>()),
            DataPath = dataNode?["Path"]?.GetValue<string>() ?? string.Empty,
            AiModel = NormalizeAiModel(
                ReadStringValue(runtimeRoot["AIModel"])
                ?? ReadStringValue(runtimeRoot["AiModel"])
                ?? ReadStringValue(runtimeAiNode?["default_model"])
                ?? ReadStringValue(runtimeAiNode?["DefaultModel"])
                ?? ReadLegacySingleModel(runtimeRoot["AI"])
                ?? ReadStringValue(root["AIModel"])
                ?? ReadStringValue(root["AiModel"])
                ?? ReadStringValue(appSettingsAiNode?["default_model"])
                ?? ReadStringValue(appSettingsAiNode?["DefaultModel"])
                ?? ReadLegacySingleModel(root["AI"])
                ?? aiModels.FirstOrDefault()),
            AiModels = aiModels.ToArray(),
            ShowAspNetCoreLogs = serverSettingsNode?["ShowAspNetCoreLogs"]?.GetValue<bool?>() ?? false,
        };
    }

    private static string ResolveDataPath(string? configuredDataPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredDataPath))
        {
            return configuredDataPath.Trim();
        }

        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(docs, "LtfsCapybara");
    }

    private static string GetRuntimeConfigPath(string dataPath)
    {
        return Path.Combine(dataPath, "config.json");
    }

    private static int NormalizeOptionId(int? value)
    {
        var normalized = value ?? MinOptionId;
        if (normalized < MinOptionId || normalized > MaxOptionId)
        {
            throw new ArgumentException($"Option id must be between {MinOptionId} and {MaxOptionId}.");
        }

        return normalized;
    }

    private static string NormalizeAiModel(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DefaultAiModel : value.Trim();
    }

    private static void WritePreferredAiModel(JsonObject runtimeRoot, string aiModel)
    {
        if (runtimeRoot["AI"] is JsonObject runtimeAiNode && runtimeAiNode["model"] is JsonValue)
        {
            runtimeAiNode["model"] = aiModel;
            runtimeRoot.Remove("AIModel");
            runtimeRoot.Remove("AiModel");
            return;
        }

        runtimeRoot["AIModel"] = aiModel;
    }

    private static IReadOnlyList<string> ReadAvailableAiModels(JsonObject runtimeRoot, JsonObject appSettingsRoot)
    {
        var models = new List<string>();
        AppendAvailableAiModels(runtimeRoot["AI"], models);
        if (models.Count == 0)
        {
            AppendAvailableAiModels(appSettingsRoot["AI"], models);
        }

        if (models.Count == 0)
        {
            var fallbackModel = ReadStringValue(runtimeRoot["AIModel"])
                ?? ReadStringValue(appSettingsRoot["AIModel"]);
            if (!string.IsNullOrWhiteSpace(fallbackModel))
            {
                models.Add(fallbackModel.Trim());
            }
        }

        return models.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static void AppendAvailableAiModels(JsonNode? aiNode, ICollection<string> models)
    {
        switch (aiNode)
        {
            case JsonObject aiObject:
                AppendModelValues(aiObject["model"] ?? aiObject["Model"], models);
                AppendModelValues(aiObject["models"] ?? aiObject["Models"], models);
                break;
            case JsonArray aiArray:
                foreach (var providerNode in aiArray)
                {
                    if (providerNode is JsonObject providerObject)
                    {
                        AppendModelValues(providerObject["model"] ?? providerObject["Model"], models);
                        AppendModelValues(providerObject["models"] ?? providerObject["Models"], models);
                    }
                }
                break;
        }
    }

    private static void AppendModelValues(JsonNode? modelNode, ICollection<string> models)
    {
        switch (modelNode)
        {
            case JsonValue modelValue:
            {
                var value = modelValue.GetValue<string>().Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    models.Add(value);
                }
                break;
            }
            case JsonArray modelArray:
                foreach (var item in modelArray)
                {
                    var value = ReadStringValue(item);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        models.Add(value.Trim());
                    }
                }
                break;
        }
    }

    private static string? ReadLegacySingleModel(JsonNode? aiNode)
    {
        if (aiNode is not JsonObject aiObject)
        {
            return null;
        }

        var modelNode = aiObject["model"] ?? aiObject["Model"];
        return modelNode is JsonValue ? ReadStringValue(modelNode) : null;
    }

    private static string? ReadStringValue(JsonNode? node)
    {
        return node is JsonValue value ? value.GetValue<string>() : null;
    }
}

public sealed class ServerSettingsDto
{
    public int IndexOnDataPartitionId { get; set; }
    public int IndexOnIndexPartitionId { get; set; }
    public string DataPath { get; set; } = string.Empty;
    public string AiModel { get; set; } = "deepseek-chat";
    public string[] AiModels { get; set; } = Array.Empty<string>();
    public bool ShowAspNetCoreLogs { get; set; }
}

public sealed class ServerSettingsUpdateRequest
{
    public int IndexOnDataPartitionId { get; set; }
    public int IndexOnIndexPartitionId { get; set; }
    public string? DataPath { get; set; }
    public string? AiModel { get; set; }
    public bool ShowAspNetCoreLogs { get; set; }
}