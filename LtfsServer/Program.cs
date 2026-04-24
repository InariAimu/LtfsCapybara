using System.Reflection;
using LtfsServer;
using System.IO;
using LtfsServer.BootStrap;
using LtfsServer.Features.TapeDrives;
using LtfsServer.Features.LocalTapes;
using LtfsServer.Features.LocalIndex;
using LtfsServer.Features.LocalFileSystem;
using LtfsServer.Features.ServerSettings;
using LtfsServer.Features.Tasks;
using LtfsServer.Features.Overview;
using LtfsServer.Features.AI;
using LtfsServer.Features.AI.Tools;
using LtfsServer.Features.Test;

var builder = WebApplication.CreateBuilder(args);

// Centralize startup configuration (URLs, data path, DI registrations)
StartupConfig.Configure(builder);

// Allow Vite dev server to call the API during development
builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowVite", p => p
		.WithOrigins("http://localhost:1420")
		.AllowAnyHeader()
		.AllowAnyMethod()
		.AllowCredentials());
});

// Register tape drive registry as app-wide singleton
builder.Services.AddSingleton<ITapeDriveRegistry, TapeDriveRegistry>();
builder.Services.AddSingleton<TapeDriveService>();
builder.Services.AddSingleton<ITapeDriveService>(sp => sp.GetRequiredService<TapeDriveService>());
// Register local tape registry (scans AppData.Path/local)
builder.Services.AddSingleton<ILocalTapeRegistry, LocalTapeRegistry>();
// Register local filesystem tree service (local drives + LAN shares)
builder.Services.AddSingleton<ILocalFileSystemTreeService, LocalFileSystemTreeService>();
// Register server settings service (read/write appsettings.json)
builder.Services.AddSingleton<IServerSettingsService, ServerSettingsService>();
builder.Services.AddSingleton<ITaskGroupService, TaskGroupService>();
builder.Services.AddSingleton<ITaskExecutionService, TaskExecutionService>();
builder.Services.AddSingleton<ILocalIndexQueryService, LocalIndexQueryService>();
builder.Services.AddSingleton<IOverviewService, OverviewService>();

builder.Services.AddHttpClient("AiServerProxy");
builder.Services.AddAiToolModules(typeof(Program).Assembly);

builder.Services.AddSingleton<IAiProviderConfigService, AiProviderConfigService>();
builder.Services.AddSingleton<IAiToolCallService, AiToolCallService>();
builder.Services.AddSingleton<IAiToolSelectionService, AiToolSelectionService>();
builder.Services.AddSingleton<IAiChatProxyService, AiChatProxyService>();

var app = builder.Build();

var ltfsLoggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
Ltfs.Log.SetLogger(new LtfsServerLoggerBridge(ltfsLoggerFactory.CreateLogger("Ltfs")));

// Apply CORS policy so the Vite dev server can access the API
app.UseCors("AllowVite");

if (app.Environment.IsDevelopment())
{
	app.UseDeveloperExceptionPage();
}

app.MapGet("/", () => Results.Ok(new { message = "LtfsServer running", env = app.Environment.EnvironmentName, url = $"{builder.WebHost}" }));

app.MapGet("/api/health", () => Results.Ok(new { status = "OK", timestamp = DateTime.UtcNow }))
   .WithName("Health");

app.MapGet("/api/info", () => Results.Ok(new { name = "LtfsServer", version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown" }))
   .WithName("Info");
 
app.MapGet("/api/example/files", () =>
{
	var files = new[] {
		new { Name = "example1.txt", Size = 1234L },
		new { Name = "example2.bin", Size = 987654L }
	};
	return Results.Ok(files);
}).WithName("GetExampleFiles");

// Register TapeDrive API endpoints
app.MapTapeDriveApi();

// Register LocalTapes API endpoints
app.MapLocalTapesApi();
// Register LocalIndex API endpoints (load and return LTFS index contents)
app.MapLocalIndexApi();
// Register host local filesystem API endpoints
app.MapLocalFileSystemApi();
// Register server settings API endpoints
app.MapServerSettingsApi();
// Register task group API endpoints
app.MapTasksApi();
// Register overview API endpoint
app.MapOverviewApi();
// Register AI chat proxy API endpoints
app.MapAiApi();
// Register test/demo endpoints
app.MapTestApi();

// Initialize local tape registry from AppData.Path/local before starting
var localRegistry = app.Services.GetRequiredService<ILocalTapeRegistry>();
var appData = app.Services.GetRequiredService<AppData>();
try
{
	var localPath = Path.Combine(appData.Path, "local");
	await localRegistry.InitializeAsync(localPath);
}
catch (Exception ex)
{
	var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LocalTapeInit");
	logger.LogWarning(ex, "Failed to initialize LocalTapeRegistry; continuing with empty list.");
}

app.Run();
