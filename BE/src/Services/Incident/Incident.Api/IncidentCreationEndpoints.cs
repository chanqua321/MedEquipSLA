using System.Security.Claims;
using MedEquipSLA.Incident.Application.Features.Incidents;
using Microsoft.AspNetCore.Mvc;

namespace MedEquipSLA.Incident.Api;

internal static class IncidentCreationEndpoints
{
    internal static void MapIncidentCreation(this WebApplication app) =>
        app.MapPost("/api/incidents", async (CreateIncidentInput input, [FromHeader(Name = "Idempotency-Key")] string? key,
            HttpContext http, CreateIncidentHandler command, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Results.Forbid();
            if (!Guid.TryParseExact(key, "D", out var request) || request == Guid.Empty) return Results.BadRequest();
            var result = await command.HandleAsync(input, actor, request, http.User.IsInRole("Admin"), http.Request.Headers.Authorization.ToString(), ct);
            return result.Status switch
            {
                IncidentCreationStatus.Created => Results.Created($"/api/incidents?id={result.Id}", new { id = result.Id }),
                IncidentCreationStatus.Invalid => Results.BadRequest(), IncidentCreationStatus.NotFound => Results.NotFound(),
                IncidentCreationStatus.Conflict when result.Message is { } message => Results.Problem(statusCode: 409, title: message),
                IncidentCreationStatus.Conflict => Results.Conflict(), _ => Results.StatusCode(503)
            };
        }).WithTags("Incidents").RequireAuthorization(policy => policy.RequireRole("Admin", "DepartmentStaff"));
}
