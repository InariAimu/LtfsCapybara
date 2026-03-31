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
            return ExtractSettings(root);
        }
    }

    public ServerSettingsDto Save(ServerSettingsUpdateRequest request)
    {
        var dataPartitionId = NormalizeOptionId(request.IndexOnDataPartitionId);
        var indexPartitionId = NormalizeOptionId(request.IndexOnIndexPartitionId);
        var dataPath = request.DataPath?.Trim() ?? string.Empty;

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

            var dataNode = root["Data"] as JsonObject;
            if (dataNode is null)
            {
                dataNode = new JsonObject();
                root["Data"] = dataNode;
            }

            // Reuse existing Data:Path for runtime behavior and expose it in settings UI.
            dataNode["Path"] = dataPath;

            WriteSettingsRoot(root);

            return new ServerSettingsDto
            {
                IndexOnDataPartitionId = dataPartitionId,
                IndexOnIndexPartitionId = indexPartitionId,
                DataPath = dataPath,
            };
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

    private static ServerSettingsDto ExtractSettings(JsonObject root)
    {
        var serverSettingsNode = root["ServerSettings"] as JsonObject;
        var dataNode = root["Data"] as JsonObject;

        return new ServerSettingsDto
        {
            IndexOnDataPartitionId = NormalizeOptionId(serverSettingsNode?["IndexOnDataPartitionId"]?.GetValue<int?>()),
            IndexOnIndexPartitionId = NormalizeOptionId(serverSettingsNode?["IndexOnIndexPartitionId"]?.GetValue<int?>()),
            DataPath = dataNode?["Path"]?.GetValue<string>() ?? string.Empty,
        };
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
}

public sealed class ServerSettingsDto
{
    public int IndexOnDataPartitionId { get; set; }
    public int IndexOnIndexPartitionId { get; set; }
    public string DataPath { get; set; } = string.Empty;
}

public sealed class ServerSettingsUpdateRequest
{
    public int IndexOnDataPartitionId { get; set; }
    public int IndexOnIndexPartitionId { get; set; }
    public string? DataPath { get; set; }
}