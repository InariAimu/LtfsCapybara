namespace LtfsServer.Features.AI;

public interface IAiProviderConfigService
{
    string GetDefaultModel();
    IReadOnlyList<string> GetAvailableModels();
    AiProviderResolution ResolveForModel(string? requestedModel);
}

public sealed class AiProviderConfigService : IAiProviderConfigService
{
    private const string DefaultFallbackModel = "deepseek-chat";

    private readonly IConfiguration _configuration;

    public AiProviderConfigService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetDefaultModel()
    {
        var configuredDefault = NormalizeModel(
            _configuration["AIModel"]
            ?? _configuration["AiModel"]
            ?? _configuration["AI:default_model"]
            ?? _configuration["AI:DefaultModel"]);

        if (!string.IsNullOrWhiteSpace(configuredDefault))
        {
            return configuredDefault;
        }

        var firstConfiguredModel = GetAvailableModels().FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstConfiguredModel) ? DefaultFallbackModel : firstConfiguredModel;
    }

    public IReadOnlyList<string> GetAvailableModels()
    {
        return ReadProviders()
            .SelectMany(provider => provider.Models)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public AiProviderResolution ResolveForModel(string? requestedModel)
    {
        var providers = ReadProviders();
        var model = NormalizeModel(requestedModel) ?? GetDefaultModel();

        if (providers.Count == 0)
        {
            throw new InvalidOperationException("AI config is missing. Please set AI entries with base_url, api_key, and model in {Data.Path}/config.json.");
        }

        var matchedProvider = providers.FirstOrDefault(provider =>
            provider.Models.Any(configuredModel => string.Equals(configuredModel, model, StringComparison.Ordinal)));

        if (matchedProvider is not null)
        {
            return new AiProviderResolution(model, matchedProvider.BaseUrl, matchedProvider.ApiKey);
        }

        if (providers.Count == 1)
        {
            return new AiProviderResolution(model, providers[0].BaseUrl, providers[0].ApiKey);
        }

        throw new InvalidOperationException($"No AI provider is configured for model '{model}'.");
    }

    private IReadOnlyList<AiProviderConfig> ReadProviders()
    {
        var aiSection = _configuration.GetSection("AI");
        if (!aiSection.Exists())
        {
            return Array.Empty<AiProviderConfig>();
        }

        var children = aiSection.GetChildren().ToArray();
        if (children.Length > 0 && children.All(child => int.TryParse(child.Key, out _)))
        {
            return children
                .Select(ParseProvider)
                .Where(provider => provider is not null)
                .Cast<AiProviderConfig>()
                .ToArray();
        }

        var provider = ParseProvider(aiSection);
        return provider is null ? Array.Empty<AiProviderConfig>() : new[] { provider };
    }

    private static AiProviderConfig? ParseProvider(IConfiguration section)
    {
        var baseUrl = ReadString(section, "base_url", "BaseUrl")?.Trim();
        var apiKey = ReadString(section, "api_key", "ApiKey")?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var models = ReadStringList(section, "model", "Model", "models", "Models");
        return new AiProviderConfig(baseUrl, apiKey, models);
    }

    private static string? ReadString(IConfiguration section, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = section[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ReadStringList(IConfiguration section, params string[] keys)
    {
        foreach (var key in keys)
        {
            var direct = section[key];
            if (!string.IsNullOrWhiteSpace(direct))
            {
                return new[] { direct.Trim() };
            }

            var childSection = section.GetSection(key);
            var values = childSection
                .GetChildren()
                .Select(child => child.Value?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToArray();
            if (values.Length > 0)
            {
                return values;
            }
        }

        return Array.Empty<string>();
    }

    private static string? NormalizeModel(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record AiProviderConfig(string BaseUrl, string ApiKey, IReadOnlyList<string> Models);
}

public sealed record AiProviderResolution(string Model, string BaseUrl, string ApiKey);