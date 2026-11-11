using System.Security.Claims;
using MedEquipSLA.Maintenance.Application.Features.WorkOrders;
using Microsoft.AspNetCore.Mvc;

namespace MedEquipSLA.Maintenance.Api;

internal static class WorkOrderOperationEndpoints
{
    internal static void MapWorkOrderOperations(this WebApplication app)
    {
        var group = app.MapGroup("/api/work-order-operations").WithTags("WorkOrder D3 operations")
            .RequireAuthorization(p => p.RequireRole("Admin"));
        group.MapGet("", async (Guid? equipmentId, string? state, int? page, IWorkOrderOperationAdmin store, CancellationToken ct) =>
        {
            if (page is < 1 or > 1_000_000 || state is not (null or "Pending" or "Committed" or "Aborted")) return Results.BadRequest();
            return Results.Ok(await store.ListAsync(equipmentId, state, page ?? 1, ct));
        });
        group.MapGet("/{id:guid}", async (Guid id, IWorkOrderOperationAdmin store, CancellationToken ct) =>
            await store.FindAsync(id, ct) is { } value ? Results.Ok(value) : Results.NotFound());
        foreach (var action in new[] { "Retry", "Cancel" })
        {
            var command = action;
            group.MapPost("/{id:guid}/" + action.ToLowerInvariant(), async (Guid id, Input input, HttpContext http,
                [FromHeader(Name = "Idempotency-Key")] string? key, IWorkOrderOperationAdmin store, CancellationToken ct) =>
            {
                if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Results.Forbid();
                if (!Guid.TryParseExact(key, "D", out var request) || request == Guid.Empty || input.RowVersion is not { Length: 8 } ||
                    input.Reason?.Trim().Length is not (> 0 and <= 2000)) return Results.BadRequest();
                var result = await store.ExecuteAsync(id, command, actor, request, input.RowVersion, input.Reason, ct);
                return result.Status switch
                {
                    OperationCommandStatus.Saved => Results.Accepted($"/api/work-order-operations/{id}", result.Value),
                    OperationCommandStatus.NotFound => Results.NotFound(),
                    _ => Results.Conflict()
                };
            });
        }
    }
    internal sealed record Input(string? Reason, byte[]? RowVersion);
}
