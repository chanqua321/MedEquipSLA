using System.Security.Claims;
using System.Text.Json;
using MedEquipSLA.Maintenance.Application.Features.WorkOrders;
using Microsoft.AspNetCore.Mvc;

namespace MedEquipSLA.Maintenance.Api;

internal static class WorkOrderWorkflowEndpoints
{
    internal static void MapWorkOrderWorkflow(this WebApplication app)
    {
        var routes = app.MapGroup("/api/work-orders").WithTags("WorkOrder execution")
            .RequireAuthorization(p => p.RequireRole("Admin", "Technician"));
        routes.MapPost("/{id:guid}/progress", async (Guid id, ProgressInput input, HttpContext http, IWorkOrderWorkflow workflow, CancellationToken ct) =>
            !Actor(http, out var actor) ? Results.Forbid() : input.RowVersion is not { Length: 8 } || input.Notes?.Trim().Length is not (> 0 and <= 2000)
                ? Results.BadRequest() : Reply(await workflow.ProgressAsync(id, actor, input.RowVersion, input.Notes, ct)))
            .RequireAuthorization(p => p.RequireRole("Technician"));
        routes.MapPost("/{id:guid}/start", async (Guid id, VersionInput input, HttpContext http, IWorkOrderWorkflow workflow, CancellationToken ct) =>
            !Actor(http, out var actor) ? Results.Forbid() : input.RowVersion is not { Length: 8 } ? Results.BadRequest() :
                Reply(await workflow.StartAsync(id, actor, input.RowVersion, ct)))
            .RequireAuthorization(p => p.RequireRole("Technician"));
        routes.MapPost("/{id:guid}/attempts", SubmitAsync).RequireAuthorization(p => p.RequireRole("Technician"))
            .DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(5 * 1024 * 1024 + 64 * 1024));
        routes.MapPost("/{id:guid}/review", async (Guid id, ReviewInput input, HttpContext http, IWorkOrderWorkflow workflow, CancellationToken ct) =>
            !Actor(http, out var actor) ? Results.Forbid() : input.RowVersion is not { Length: 8 } ||
                input.Reason?.Length > 2000 || !input.Accept && string.IsNullOrWhiteSpace(input.Reason) ? Results.BadRequest() :
                Reply(await workflow.ReviewAsync(id, actor, input.RowVersion, input.Accept, input.Reason, ct)))
            .RequireAuthorization(p => p.RequireRole("Admin"));
        routes.MapPost("/{id:guid}/cancel", async (Guid id, CancelInput input, HttpContext http, IWorkOrderWorkflow workflow, CancellationToken ct) =>
            !Actor(http, out var actor) ? Results.Forbid() : input.RowVersion is not { Length: 8 } ||
                input.Reason?.Trim().Length is not (> 0 and <= 2000) ? Results.BadRequest() :
                Reply(await workflow.CancelAsync(id, actor, input.RowVersion, input.Reason, ct)))
            .RequireAuthorization(p => p.RequireRole("Admin"));
        routes.MapGet("/{id:guid}/attempts/{attempt:guid}/photo", async (Guid id, Guid attempt, HttpContext http,
            IWorkOrderReader reader, CancellationToken ct) =>
        {
            if (!Actor(http, out var actor)) return Results.Forbid();
            var photo = await reader.PhotoAsync(id, attempt, actor, http.User.IsInRole("Admin"), ct);
            if (photo is null) return Results.NotFound();
            http.Response.Headers.CacheControl = "private, no-store"; http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(photo.Bytes, photo.ContentType);
        });
        routes.MapGet("/{id:guid}/attempts/{attempt:guid}/measurements", async (Guid id, Guid attempt, HttpContext http,
            IWorkOrderReader reader, CancellationToken ct) =>
        {
            if (!Actor(http, out var actor)) return Results.Forbid();
            return await reader.MeasurementsAsync(id, attempt, actor, http.User.IsInRole("Admin"), ct) is { } rows ? Results.Ok(rows) : Results.NotFound();
        });
    }

    private static async Task<IResult> SubmitAsync(Guid id, [FromForm] string notes, [FromForm] string result,
        [FromForm] string rowVersion, [FromForm] string? measurementsJson, [FromForm] decimal? cost, [FromForm] string? currency,
        IFormFile photo, HttpContext http, IWorkOrderWorkflow workflow, CancellationToken ct)
    {
        if (!Actor(http, out var actor)) return Results.Forbid();
        if (notes.Trim().Length is < 1 or > 4000 || result is not ("Passed" or "Failed") ||
            photo.Length is < 1 or > 5242880 || measurementsJson?.Length > 60000) return Results.BadRequest();
        var version = new byte[8];
        if (!Convert.TryFromBase64String(rowVersion, version, out var written) || written != 8) return Results.BadRequest();
        CalibrationInput[] measurements;
        try { measurements = JsonSerializer.Deserialize<CalibrationInput[]>(measurementsJson ?? "[]",
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? []; }
        catch (JsonException) { return Results.BadRequest(); }
        using var stream = new MemoryStream((int)photo.Length);
        await photo.CopyToAsync(stream, ct);
        var bytes = stream.ToArray();
        var type = bytes.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10}) &&
            bytes.AsSpan().EndsWith(new byte[] {73,69,78,68,174,66,96,130}) ? "image/png" :
            bytes.AsSpan().StartsWith(new byte[] {255,216,255}) && bytes.AsSpan().EndsWith(new byte[] {255,217}) ? "image/jpeg" : null;
        if (type is null) return Results.BadRequest();
        return Reply(await workflow.SubmitAsync(id, actor, version,
            new(result, notes, cost, currency?.Trim().ToUpperInvariant(), bytes, type, measurements), ct));
    }
    private static bool Actor(HttpContext http, out Guid actor) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out actor) && actor != Guid.Empty;
    private static IResult Reply(WorkflowStatus status) => status switch
    {
        WorkflowStatus.Saved => Results.NoContent(), WorkflowStatus.NotFound => Results.NotFound(),
        WorkflowStatus.Forbidden => Results.Forbid(), WorkflowStatus.Invalid => Results.BadRequest(),
        WorkflowStatus.Unavailable => Results.StatusCode(503), _ => Results.Conflict()
    };
    internal sealed record VersionInput(byte[]? RowVersion);
    internal sealed record ProgressInput(byte[]? RowVersion, string? Notes);
    internal sealed record ReviewInput(byte[]? RowVersion, bool Accept, string? Reason);
    internal sealed record CancelInput(byte[]? RowVersion, string? Reason);
}
