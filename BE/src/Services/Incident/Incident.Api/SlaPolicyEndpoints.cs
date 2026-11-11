using System.Security.Claims;
using MedEquipSLA.Incident.Application.Features.Sla;

namespace MedEquipSLA.Incident.Api;

internal static class SlaPolicyEndpoints
{
    public static void MapSlaPolicies(this WebApplication app)
    {
        var routes = app.MapGroup("/api/sla-policies").WithTags("SLA Policies").RequireAuthorization(p => p.RequireRole("Admin"));
        routes.MapGet("", async (SlaPolicyQueries query, CancellationToken ct) => Results.Ok(await query.HandleAsync(ct)));
        routes.MapPut("/{priority}", async (string priority, SlaPolicyInput input, HttpContext http, SlaPolicyCommands command, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Results.Forbid();
            return await command.HandleAsync(priority, input, actor, http.TraceIdentifier, ct) switch
            { SlaPolicyResult.Saved => Results.NoContent(), SlaPolicyResult.Invalid => Results.BadRequest(), _ => Results.Conflict() };
        });
    }
}
