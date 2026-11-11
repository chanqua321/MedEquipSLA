using System.Security.Claims;
using MedEquipSLA.Application.Coordination;
using Microsoft.AspNetCore.Mvc;

namespace MedEquipSLA.Incident.Api;

internal static class SourceOperationEndpoints
{
    internal static void MapSourceOperations(this WebApplication app)
    {
        var routes = app.MapGroup("/api/incident-operations")
            .WithTags("Incident creation operations").RequireAuthorization(p => p.RequireRole("Admin"));
        routes.MapGet("", async (Guid? equipment, int? page, ISourceCreationOperations store, CancellationToken ct) =>
            page is < 1 or > 1000000 ? Results.BadRequest() : Results.Ok(await store.ListAsync(equipment, page ?? 1, ct)));
        routes.MapGet("/{id:guid}", async (Guid id, ISourceCreationOperations store, CancellationToken ct) =>
            await store.FindAsync(id, ct) is { } row ? Results.Ok(row) : Results.NotFound());
        foreach (var action in new[] { "Cancel", "Retry" })
        {
            var command = action;
            routes.MapPost("/{id:guid}/" + action.ToLowerInvariant(), async (Guid id, Input input, HttpContext http,
                [FromHeader(Name = "Idempotency-Key")] string? key, ISourceCreationOperations store, CancellationToken ct) =>
            {
                if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Results.Forbid();
                if (!Guid.TryParseExact(key, "D", out var request) || request == Guid.Empty || input.RowVersion is not { Length: 8 } ||
                    input.Reason?.Trim().Length is not (> 0 and <= 2000)) return Results.BadRequest();
                var (status, value) = await store.ExecuteAsync(id, new(command, actor, request, input.RowVersion, input.Reason.Trim()), ct);
                return status switch { "Saved" => Results.Accepted(value: value), "NotFound" => Results.NotFound(), _ => Results.Conflict() };
            });
        }
    }
    internal sealed record Input(byte[]? RowVersion, string? Reason);
}
