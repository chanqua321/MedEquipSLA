using System.Security.Claims;
using MedEquipSLA.Equipment.Application.Features.Retirement;
using Microsoft.AspNetCore.Mvc;

namespace MedEquipSLA.Equipment.Api;

public static class RetirementEndpoints
{
    public static void MapRetirement(this WebApplication app)
    {
        app.MapGet("/api/equipment/retirement-candidates", async (string? search, int? page, IRetirementStore store, CancellationToken ct) =>
            search?.Length > 200 || page is < 1 or > 1_000_000 ? Results.BadRequest() : Results.Ok(await store.CandidatesAsync(search?.Trim(), page ?? 1, ct)))
            .WithDescription("Local candidates only. Source coordination is mandatory before retirement can succeed.")
            .WithTags("Equipment retirement").RequireAuthorization(p => p.RequireRole("Admin"));
        var routes = app.MapGroup("/api/equipment/{equipment:guid}/retirement-intents")
            .WithTags("Equipment retirement").RequireAuthorization(p => p.RequireRole("Admin"));
        routes.MapGet("", async (Guid equipment, int? page, IRetirementStore store, CancellationToken ct) =>
            page is < 1 or > 1_000_000 ? Results.BadRequest() : Results.Ok(await store.HistoryAsync(equipment, page ?? 1, ct)));
        routes.MapPost("", async (Guid equipment, BeginRetirement input,
            [FromHeader(Name = "Idempotency-Key")] string? key, HttpContext http, IRetirementStore store, CancellationToken ct) =>
        {
            if (!Actor(http, out var actor)) return Results.Forbid();
            if (!Key(key, out var idempotency) || !Reason(input.Reason)) return Results.BadRequest();
            return Reply(await store.BeginAsync(equipment, actor, idempotency, input.Reason!, http.TraceIdentifier, ct));
        });
        routes.MapGet("/{intent:guid}", async (Guid equipment, Guid intent, IRetirementStore store, CancellationToken ct) =>
            await store.FindAsync(equipment, intent, ct) is { } value ? Results.Ok(value) : Results.NotFound());
        foreach (var action in new[] { "Continue", "Cancel" })
        {
            var command = action;
            routes.MapPost("/{intent:guid}/" + action.ToLowerInvariant(), async (Guid equipment, Guid intent, RetirementCommand input,
                [FromHeader(Name = "Idempotency-Key")] string? key, HttpContext http, IRetirementStore store, CancellationToken ct) =>
            {
                if (!Actor(http, out var actor)) return Results.Forbid();
                if (!Key(key, out var idempotency) || !Reason(input.Reason) || input.RowVersion is not { Length: 8 })
                    return Results.BadRequest();
                return Reply(await store.AdminAsync(equipment, intent, actor, idempotency, input.RowVersion, command,
                    input.Reason!, http.TraceIdentifier, ct));
            });
        }
    }

    private static IResult Reply(RetirementResult result) => result.Outcome switch
    {
        RetirementOutcome.NotFound => Results.NotFound(),
        RetirementOutcome.Conflict => Results.Conflict(),
        RetirementOutcome.Invalid => Results.BadRequest(),
        _ when result.Value!.State is "Completed" or "Cancelled" => Results.Ok(result.Value),
        _ => Results.Accepted($"/api/equipment/{result.Value!.EquipmentId}/retirement-intents/{result.Value.Id}", result.Value)
    };
    private static bool Actor(HttpContext http, out Guid actor) =>
        Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out actor) && actor != Guid.Empty;
    private static bool Key(string? input, out Guid key) => Guid.TryParseExact(input, "D", out key) && key != Guid.Empty;
    private static bool Reason(string? value) => value?.Trim().Length is > 0 and <= 2000;
    public sealed record BeginRetirement(string? Reason);
    public sealed record RetirementCommand(string? Reason, byte[]? RowVersion);
}
