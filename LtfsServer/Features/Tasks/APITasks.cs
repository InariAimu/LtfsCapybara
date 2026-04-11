using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LtfsServer.Features.Tasks;

public static class APITasks
{
    public static void MapTasksApi(this WebApplication app)
    {
        app.MapGet("/api/tasks/executions", (ITaskExecutionService executionService) =>
            Results.Ok(executionService.ListExecutions()));

        app.MapGet("/api/tasks/executions/{executionId}", (string executionId, ITaskExecutionService executionService) =>
        {
            try
            {
                return Results.Ok(executionService.GetExecution(executionId));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        app.MapPost("/api/tasks/groups/{tapeBarcode}/execute", (string tapeBarcode, ExecuteTapeFsTaskGroupRequest request, ITaskExecutionService executionService) =>
        {
            try
            {
                return Results.Ok(executionService.StartExecution(tapeBarcode, request.TapeDriveId, request.ScsiMetricsEnabled));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPut("/api/tasks/executions/{executionId}/metrics", (string executionId, UpdateTaskExecutionMetricsRequest request, ITaskExecutionService executionService) =>
        {
            try
            {
                return Results.Ok(executionService.UpdateScsiMetrics(executionId, request.Enabled));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        app.MapPost("/api/tasks/executions/{executionId}/incidents/{incidentId}/resolve", (string executionId, string incidentId, ResolveTaskExecutionIncidentRequest request, ITaskExecutionService executionService) =>
        {
            try
            {
                return Results.Ok(executionService.ResolveIncident(executionId, incidentId, request.Resolution));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        app.MapGet("/api/tasks/events", async (HttpContext context, ITaskExecutionService executionService, IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";
            context.Response.ContentType = "text/event-stream";

            await foreach (var item in executionService.SubscribeAsync(cancellationToken))
            {
                var json = JsonSerializer.Serialize(item, jsonOptions.Value.SerializerOptions);
                await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        });

        app.MapGet("/api/tasks/groups", (ITaskGroupService service) =>
            Results.Ok(service.ListGroups()));

        app.MapGet("/api/tasks/groups/{tapeBarcode}", (string tapeBarcode, ITaskGroupService service) =>
        {
            try
            {
                return Results.Ok(service.GetOrCreateGroup(tapeBarcode));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/tasks/groups/{tapeBarcode}/rename", (string tapeBarcode, RenameTapeFsTaskGroupRequest request, ITaskGroupService service) =>
        {
            try
            {
                return Results.Ok(service.RenameGroup(tapeBarcode, request.Name));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/tasks/groups/{tapeBarcode}/tasks", (string tapeBarcode, TapeFsTaskCreateRequest request, ITaskGroupService service) =>
        {
            try
            {
                return Results.Ok(service.AddTask(tapeBarcode, request));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (FileNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (DirectoryNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/tasks/groups/{tapeBarcode}/tasks/server-folder", (string tapeBarcode, AddTapeFsServerFolderTaskRequest request, ITaskGroupService service) =>
        {
            try
            {
                return Results.Ok(service.AddServerFolderTask(tapeBarcode, request));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (DirectoryNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/tasks/groups/{tapeBarcode}/tasks/format", (string tapeBarcode, AddTapeFsFormatTaskRequest? request, ITaskGroupService service) =>
        {
            try
            {
                return Results.Ok(service.AddFormatTask(tapeBarcode, request?.FormatTask));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapDelete("/api/tasks/groups/{tapeBarcode}/tasks/{taskId}", (string tapeBarcode, string taskId, ITaskGroupService service) =>
        {
            try
            {
                return Results.Ok(service.DeleteTask(tapeBarcode, taskId));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });
    }
}

