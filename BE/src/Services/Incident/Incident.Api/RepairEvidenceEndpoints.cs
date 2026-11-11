using System.Security.Claims;
using MedEquipSLA.Incident.Application.Features.Incidents;
using Microsoft.AspNetCore.Mvc;

namespace MedEquipSLA.Incident.Api;

internal static class RepairEvidenceEndpoints
{
    private const int MaximumPhotoBytes = 5 * 1024 * 1024;
    internal static void MapRepairEvidence(this WebApplication app)
    {
        var routes = app.MapGroup("/api/incidents").WithTags("Incident repairs")
            .RequireAuthorization(p => p.RequireRole("Admin", "Technician", "DepartmentStaff"));
        routes.MapGet("/", async (Guid? id, HttpContext http, IncidentRepairQueries query, CancellationToken ct) =>
            Results.Ok(await query.ListAsync(id, Department(http.User), ct)));
        routes.MapGet("/{id:guid}/attempts", async (Guid id, HttpContext http, IncidentRepairQueries query, CancellationToken ct) =>
            await query.AttemptsAsync(id, Department(http.User), ct) is { } rows ? Results.Ok(rows) : Results.NotFound());
        routes.MapGet("/{id:guid}/attempts/{attemptId:guid}/photo", async (Guid id, Guid attemptId, HttpContext http, IncidentRepairQueries query, CancellationToken ct) =>
        {
            var photo = await query.PhotoAsync(id, attemptId, Department(http.User), ct);
            if (photo is null) return Results.NotFound();
            http.Response.Headers.CacheControl = "private, no-store"; http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(photo.Bytes, photo.ContentType);
        });
        routes.MapPost("/{id:guid}/attempts", SubmitAsync).RequireAuthorization(p => p.RequireRole("Admin", "Technician"))
            .DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(MaximumPhotoBytes + 64 * 1024)).Produces(201).ProducesProblem(400).Produces(409);
    }
    private static Guid? Department(ClaimsPrincipal user) => user.IsInRole("Admin") || user.IsInRole("Technician") ? null :
        Guid.TryParse(user.FindFirstValue("department_id"), out var id) ? id : Guid.Empty;
    private static async Task<IResult> SubmitAsync(Guid id, [FromForm] string notes, [FromForm] string result,
        [FromForm] string rowVersion, IFormFile photo, HttpContext http, IncidentRepairCommands command, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Results.Forbid();
        notes = notes.Trim();
        result = result.Trim();
        var version = new byte[8];
        if (notes.Length is 0 or > 4000 || result.Length is 0 or > 2000 ||
            !Convert.TryFromBase64String(rowVersion, version, out var written) || written != 8 ||
            photo.Length is <= 0 or > MaximumPhotoBytes)
            return Results.Problem(statusCode: 400, title: "Biên bản, kết quả, phiên bản và ảnh tối đa 5 MB là bắt buộc.");

        using var buffer = new MemoryStream((int)photo.Length);
        await photo.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var contentType = PhotoType(bytes);
        if (contentType is null)
            return Results.Problem(statusCode: 400, title: "Ảnh phải có định dạng JPEG hoặc PNG.");

        var resultValue = await command.HandleAsync(id, actor, http.User.IsInRole("Admin"),
            new(notes, result, version, bytes, contentType), http.TraceIdentifier, ct);
        return resultValue.Status switch
        {
            IncidentWorkflowStatus.Saved => Results.Created($"/api/incidents/{id}/attempts/{resultValue.AttemptId}/photo", new { id = resultValue.AttemptId, attemptNumber = resultValue.AttemptNumber }),
            IncidentWorkflowStatus.NotFound => Results.NotFound(), IncidentWorkflowStatus.Forbidden => Results.Forbid(),
            IncidentWorkflowStatus.Conflict => Results.Conflict(), _ => Results.BadRequest()
        };
    }
    private static string? PhotoType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
            bytes.EndsWith(new byte[] { 73, 69, 78, 68, 174, 66, 96, 130 })) return "image/png";
        if (bytes.StartsWith(new byte[] { 255, 216, 255 }) && bytes.EndsWith(new byte[] { 255, 217 })) return "image/jpeg";
        return null;
    }
}
