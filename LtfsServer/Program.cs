using System.Reflection;
using LtfsServer;
using LtfsServer.API;
using LtfsServer.Services;

using TapeDrive;

var builder = WebApplication.CreateBuilder(args);

// Read API host/port from configuration (appsettings, env vars, or CLI args)
var apiHost = builder.Configuration["Api:Host"] ?? "localhost";
var apiPort = builder.Configuration["Api:Port"] ?? Environment.GetEnvironmentVariable("PORT") ?? "5003";
var apiScheme = builder.Configuration["Api:Scheme"] ?? "http";

builder.WebHost.UseUrls($"{apiScheme}://{apiHost}:{apiPort}");

// Register tape drive registry as app-wide singleton
builder.Services.AddSingleton<ITapeDriveRegistry, TapeDriveRegistry>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseDeveloperExceptionPage();

    var registry = app.Services.GetRequiredService<ITapeDriveRegistry>();
    registry.TryAdd("fake-1", new FakeTapeDrive());
}

app.MapGet("/", () => Results.Ok(new { message = "LtfsServer running", env = app.Environment.EnvironmentName, url = $"{apiScheme}://{apiHost}:{apiPort}" }));

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

app.Run();
