using System.Security.Claims;
using MedEquipSLA.Maintenance.Application.Features.Schedules;
using Microsoft.AspNetCore.Mvc;

namespace MedEquipSLA.Maintenance.Api;

internal static class ScheduleEndpoints
{
    public static void MapScheduleQueries(this WebApplication app)
    {
        var routes = app.MapGroup("/api/maintenance-schedules").WithTags("Maintenance schedules")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));
        routes.MapGet("", async (string? search, bool? active, int? page, IScheduleReader reader, CancellationToken ct) =>
        {
            if (search?.Length > 200 || page is < 1 or > 1_000_000) return Results.BadRequest();
            return Results.Ok(await reader.ListAsync(new(search?.Trim(), active, page ?? 1), ct));
        });
        routes.MapGet("/{id:guid}", async (Guid id, IScheduleReader reader, CancellationToken ct) =>
            await reader.FindAsync(id, ct) is { } row ? Results.Ok(row) : Results.NotFound());
        routes.MapPost("", CreateAsync).Produces<ScheduleDetail>(201).ProducesProblem(503);
        routes.MapPut("/{id:guid}", UpdateAsync).Produces<ScheduleDetail>();
        foreach (var action in new[] { "end", "end/retry-release" })
        {
            var retry = action != "end";
            routes.MapPost("/{id:guid}/" + action, async (Guid id, EndSchedule input, HttpContext http,
                [FromHeader(Name = "Idempotency-Key")] string? key, IScheduleTermination command, CancellationToken ct) =>
            {
                if (!TryActor(http, out var actor)) return Results.Forbid();
                if (!Guid.TryParseExact(key, "D", out var request) || request == Guid.Empty || input.RowVersion is not { Length: 8 } ||
                    input.Reason?.Trim().Length is not (> 0 and <= 2000)) return Results.BadRequest();
                var result = await command.ExecuteAsync(id, actor, request, input.RowVersion, input.Reason.Trim(), retry, ct);
                return result.Schedule is { } row ? Results.Accepted($"/api/maintenance-schedules/{id}", row) : Failure(result.Status);
            });
        }
        routes.MapGet("/{id:guid}/occurrences", async (Guid id, int? page, IScheduleReader reader, CancellationToken ct) =>
        {
            if (page is < 1 or > 1_000_000) return Results.BadRequest();
            return await reader.OccurrencesAsync(id, page ?? 1, ct) is { } rows ? Results.Ok(rows) : Results.NotFound();
        });
    }

    private static async Task<IResult> CreateAsync(CreateSchedule input, [FromHeader(Name = "Idempotency-Key")] string? key,
        HttpContext http, IScheduleCommands commands, CancellationToken ct)
    {
        if (!TryActor(http, out var actor)) return Results.Forbid();
        var timing = input.Timing?.Normalize();
        if (timing is null || input.EquipmentId == Guid.Empty || input.WorkType is not ("Maintenance" or "Calibration") ||
            !Guid.TryParseExact(key, "D", out var requestKey) || requestKey == Guid.Empty) return Results.BadRequest();

        var result = await commands.CreateAsync(input.EquipmentId, input.WorkType, timing, actor, requestKey,
            http.Request.Headers.Authorization.ToString(), ct);
        if (result.OperationId is { } operation) http.Response.Headers["X-Operation-Id"] = operation.ToString();
        return result.Schedule is { } row ? Results.Created($"/api/maintenance-schedules/{row.Id}", row) : Failure(result.Status);
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateSchedule input, HttpContext http,
        IScheduleCommands commands, CancellationToken ct)
    {
        if (!TryActor(http, out var actor)) return Results.Forbid();
        var timing = input.Timing?.Normalize();
        if (timing is null || input.Timing?.IsActive is null || input.RowVersion is not { Length: 8 }) return Results.BadRequest();
        var result = await commands.UpdateAsync(id, timing, input.RowVersion, actor, http.TraceIdentifier, ct);
        return result.Schedule is { } row ? Results.Ok(row) : Failure(result.Status);
    }

    private static bool TryActor(HttpContext http, out Guid actor) =>
        Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out actor) && actor != Guid.Empty;
    internal sealed record EndSchedule(byte[]? RowVersion, string? Reason);

    private static IResult Failure(ScheduleStatus status) => status switch
    {
        ScheduleStatus.NotFound => Results.NotFound(),
        ScheduleStatus.InvalidEquipment => Results.Problem(statusCode: 400, title: "Equipment is not available for registration."),
        ScheduleStatus.InvalidTiming => Results.Problem(statusCode: 400, title: "The schedule timing is outside the supported range."),
        ScheduleStatus.Unavailable => Results.Problem(statusCode: 503, title: "Retry with the same Idempotency-Key."),
        _ => Results.Conflict()
    };

    internal sealed record CreateSchedule(Guid EquipmentId, string? WorkType, TimingInput? Timing);
    internal sealed record UpdateSchedule(byte[]? RowVersion, TimingInput? Timing);
    internal sealed record TimingInput(DateTimeOffset? AnchorAtUtc, int IntervalDays, int? ReminderLeadMinutes,
        int? CompletionWindowMinutes, bool? IsActive)
    {
        public ScheduleTiming? Normalize()
        {
            if (AnchorAtUtc is not { } anchor) return null;
            var value = new ScheduleTiming(anchor.ToUniversalTime(), IntervalDays, ReminderLeadMinutes ?? 1440,
                CompletionWindowMinutes ?? 0, IsActive ?? true);
            return ScheduleTimingLimits.IsUsable(value, value.AnchorAtUtc) ? value : null;
        }
    }
}
