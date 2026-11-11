using System.Security.Claims;
using MedEquipSLA.Incident.Application.Features.Incidents;

namespace MedEquipSLA.Incident.Api;

internal static class IncidentQueryEndpoints
{
    internal static void MapIncidentQueries(this WebApplication app)
    {
        var routes = app.MapGroup("/api/incidents").WithTags("Incidents")
            .RequireAuthorization(p => p.RequireRole("Admin", "Technician", "DepartmentStaff"));
        routes.MapGet("/search", async (string? search, string? status, string? priority, Guid? equipmentId,
            Guid? technicianId, string? sort, int? page, int? pageSize, HttpContext http, IncidentQueries queries, CancellationToken ct) =>
        {
            if (search?.Length > 200 || status is not (null or "Open" or "InProgress" or "Resolved" or "Closed") ||
                priority is not (null or "Critical" or "High" or "Medium" or "Low") ||
                sort is not (null or "newest" or "oldest" or "deadline" or "code") ||
                page is < 1 or > 1_000_000 || pageSize is < 1 or > 100) return Results.BadRequest();
            return Results.Ok(await queries.SearchAsync(new(search?.Trim(), status, priority, equipmentId,
                technicianId, sort ?? "newest", page ?? 1, pageSize ?? 25, Department(http.User)), ct));
        });
        routes.MapGet("/{id:guid}", async (Guid id, HttpContext http, IncidentQueries queries, CancellationToken ct) =>
            await queries.FindAsync(id, Department(http.User), ct) is { } row ? Results.Ok(row) : Results.NotFound());
    }
    private static Guid? Department(ClaimsPrincipal user) => user.IsInRole("Admin") || user.IsInRole("Technician")
        ? null : Guid.TryParse(user.FindFirstValue("department_id"), out var id) ? id : Guid.Empty;
}
