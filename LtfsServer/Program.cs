using System.Reflection;
using LtfsServer;
using LtfsServer.API;
using LtfsServer.Services;
using System.IO;

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
builder.Services.AddSingleton<ITapeDriveService, TapeDriveService>();
builder.Services.AddSingleton<ITapeMachineService, TapeMachineService>();
// Register local tape registry (scans AppData.Path/local)
builder.Services.AddSingleton<ILocalTapeRegistry, LocalTapeRegistry>();
// Register local filesystem tree service (local drives + LAN shares)
builder.Services.AddSingleton<ILocalFileSystemTreeService, LocalFileSystemTreeService>();

var app = builder.Build();

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
