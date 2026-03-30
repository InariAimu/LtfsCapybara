using Microsoft.AspNetCore.Builder;
using LtfsServer.Services;

namespace LtfsServer.API;

public static class APITasks
{
    public static void MapTasksApi(this WebApplication app)
    {
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

        app.MapPost("/api/tasks/groups/{tapeBarcode}/rename", (string tapeBarcode, RenameTaskGroupRequest request, ITaskGroupService service) =>
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

        app.MapPost("/api/tasks/groups/{tapeBarcode}/tasks", (string tapeBarcode, LtfsTaskCreateRequest request, ITaskGroupService service) =>
        {
            try
            {
                return Results.Ok(service.AddTask(tapeBarcode, request));
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

        app.MapPost("/api/tasks/groups/{tapeBarcode}/tasks/format", (string tapeBarcode, AddFormatTaskRequest? request, ITaskGroupService service) =>
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
