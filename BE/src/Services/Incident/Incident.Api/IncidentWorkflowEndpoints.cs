using System.Security.Claims;
using MedEquipSLA.Incident.Application.Features.Incidents;

namespace MedEquipSLA.Incident.Api;

internal static class IncidentWorkflowEndpoints
{
    internal static void MapIncidentWorkflow(this WebApplication app)
    {
        var routes = app.MapGroup("/api/incidents/{id:guid}").WithTags("Incident repairs");
        routes.MapPut("/assignment", async (Guid id, IncidentAssignmentInput input, HttpContext http, IncidentWorkflowCommands command, CancellationToken ct) =>
            Actor(http) is { } actor ? Reply(await command.AssignAsync(id, input, actor, http.Request.Headers.Authorization.ToString(), http.TraceIdentifier, ct)) : Results.Forbid())
            .RequireAuthorization(p => p.RequireRole("Admin"));
        routes.MapPost("/start", async (Guid id, IncidentVersionInput input, HttpContext http, IncidentWorkflowCommands command, CancellationToken ct) =>
            Actor(http) is { } actor ? Reply(await command.StartAsync(id, input, actor, http.TraceIdentifier, ct)) : Results.Forbid())
            .RequireAuthorization(p => p.RequireRole("Technician"));
        routes.MapPost("/attempts/{attemptId:guid}/reject", async (Guid id, Guid attemptId, IncidentRejectionInput input, HttpContext http, IncidentWorkflowCommands command, CancellationToken ct) =>
            Actor(http) is { } actor ? Reply(await command.RejectAsync(id, attemptId, input, actor, http.TraceIdentifier, ct)) : Results.Forbid())
            .RequireAuthorization(p => p.RequireRole("Admin"));
        routes.MapPost("/attempts/{attemptId:guid}/accept", async (Guid id, Guid attemptId, IncidentVersionInput input, HttpContext http, IncidentWorkflowCommands command, CancellationToken ct) =>
            Actor(http) is { } actor ? Reply(await command.AcceptAsync(id, attemptId, input, actor, http.TraceIdentifier, ct)) : Results.Forbid())
            .RequireAuthorization(p => p.RequireRole("Admin"));
    }
    private static Guid? Actor(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty ? id : null;
    private static IResult Reply(IncidentWorkflowStatus status) => status switch
    {
        IncidentWorkflowStatus.Saved => Results.NoContent(), IncidentWorkflowStatus.Invalid => Results.BadRequest(),
        IncidentWorkflowStatus.NotFound => Results.NotFound(), IncidentWorkflowStatus.Forbidden => Results.Forbid(),
        IncidentWorkflowStatus.Conflict => Results.Conflict(), _ => Results.StatusCode(503)
    };
}
