using System;
using System.IO;
using Microsoft.AspNetCore.Builder;

namespace LtfsServer.BootStrap;

public static class StartupConfig
{
    public static void Configure(WebApplicationBuilder builder)
    {
        // Read API host/port from configuration (appsettings, env vars, or CLI args)
        var apiHost = builder.Configuration["Api:Host"] ?? "localhost";
        var apiPort = builder.Configuration["Api:Port"] ?? Environment.GetEnvironmentVariable("PORT") ?? "5003";
        var apiScheme = builder.Configuration["Api:Scheme"] ?? "http";

        builder.WebHost.UseUrls($"{apiScheme}://{apiHost}:{apiPort}");

        // Resolve application data path from configuration or use Documents/LtfsCapybara
        var configuredDataPath = builder.Configuration["Data:Path"];
        string dataPath;
        if (!string.IsNullOrWhiteSpace(configuredDataPath))
        {
            dataPath = configuredDataPath!;
        }
        else
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            dataPath = Path.Combine(docs, "LtfsCapybara");
        }

        Directory.CreateDirectory(dataPath);

        // Load runtime overrides from {Data.Path}/config.json (e.g., AI credentials).
        // Added after appsettings so values here take precedence.
        var runtimeConfigPath = Path.Combine(dataPath, "config.json");
        builder.Configuration.AddJsonFile(runtimeConfigPath, optional: true, reloadOnChange: true);

        // Register resolved data path for DI consumers
        builder.Services.AddSingleton(new AppData { Path = dataPath });
    }
}
