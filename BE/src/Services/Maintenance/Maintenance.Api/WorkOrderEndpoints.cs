using System.Security.Claims;
using MedEquipSLA.Maintenance.Application.Features.WorkOrders;

namespace MedEquipSLA.Maintenance.Api;

internal static class WorkOrderEndpoints
{
    public static void MapWorkOrders(this WebApplication app)
    {
        var routes = app.MapGroup("/api/work-orders").WithTags("Work orders")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Technician"));
        routes.MapGet("", async (string? search, string? status, int? page, IWorkOrderReader reader, CancellationToken ct) =>
        {
            if (search?.Length > 200 || page is < 1 or > 1_000_000 ||
                status is not (null or "Scheduled" or "InProgress" or "Completed" or "Closed" or "Cancelled"))
                return Results.BadRequest();
            return Results.Ok(await reader.ListAsync(new(search?.Trim(), status, page ?? 1), ct));
        });
        routes.MapGet("/{id:guid}", async (Guid id, IWorkOrderReader reader, CancellationToken ct) =>
            await reader.FindAsync(id, ct) is { } row ? Results.Ok(row) : Results.NotFound());
        routes.MapGet("/{id:guid}/attempts", async (Guid id, IWorkOrderReader reader, CancellationToken ct) =>
            await reader.ReadAttemptsAsync(id, ct) is { } rows ? Results.Ok(rows) : Results.NotFound());
        routes.MapPut("/{id:guid}/assignment", AssignAsync).RequireAuthorization(policy => policy.RequireRole("Admin"));
    }

    private static async Task<IResult> AssignAsync(Guid id, Assignment input, HttpContext http,
        WorkOrderAssignmentCommand command, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Results.Forbid();
        return await command.HandleAsync(id, input.TechnicianId, input.RowVersion, actor, http.Request.Headers.Authorization.ToString(), ct) switch
        {
            AssignmentResult.Saved => Results.NoContent(), AssignmentResult.NotFound => Results.NotFound(),
            AssignmentResult.Invalid => Results.BadRequest(), AssignmentResult.Unavailable => Results.StatusCode(503), _ => Results.Conflict()
        };
    }

    internal sealed record Assignment(Guid TechnicianId, byte[]? RowVersion);
}
