using System.Security.Claims;
using MedEquipSLA.Identity.Application.Abstractions;

namespace MedEquipSLA.Identity.Api;

internal static class EmployeeDirectory
{
    public static void MapEmployeeDirectory(this WebApplication app)
    {
        var users = app.MapGroup("/api/users").RequireAuthorization(p => p.RequireRole("Admin"));
        users.AddEndpointFilter(async (context, next) =>
        {
            var actor = SessionClaims.ReadClaims(context.HttpContext.User);
            return actor is not null && await context.HttpContext.RequestServices.GetRequiredService<IIdentitySessions>().IsCurrentAsync(actor, context.HttpContext.RequestAborted)
                ? await next(context) : Results.Unauthorized();
        });
        users.MapGet("/technicians/{id:guid}/eligibility", async (Guid id, HttpContext http, EmployeeQueries queries, CancellationToken ct) =>
        {
            var version = await queries.TechnicianVersionAsync(id, ct);
            if (version is null) return Results.NotFound();
            http.Response.Headers["X-Authorization-Version"] = version.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Results.NoContent();
        });
        users.MapGet("/", async (string? search, int? page, EmployeeQueries query, CancellationToken ct) =>
            search?.Length > 200 || page is < 1 or > 100000 ? Results.BadRequest() : Results.Ok(await query.ListAsync(search, page ?? 1, ct)));
        users.MapPut("/{id:guid}/access", async (Guid id, EmployeeAccessInput input, HttpContext http, EmployeeCommands command, CancellationToken ct) =>
            SessionClaims.ReadClaims(http.User) is { } actor ? Reply(await command.AccessAsync(id, input, actor, http.TraceIdentifier, ct)) : Results.Unauthorized());
        users.MapPut("/{id:guid}/profile", async (Guid id, EmployeeProfileInput input, HttpContext http, EmployeeCommands command, IHttpClientFactory clients, CancellationToken ct) =>
        {
            if (SessionClaims.ReadClaims(http.User) is not { } actor) return Results.Unauthorized();
            var department = await AccountProvisioningEndpoints.DepartmentAsync(input.DepartmentId, http, clients, ct);
            return Reply(await command.ProfileAsync(id, input, department, actor, http.TraceIdentifier, ct));
        });
    }
    private static IResult Reply(EmployeeChangeStatus status) => status switch
    {
        EmployeeChangeStatus.Saved => Results.NoContent(), EmployeeChangeStatus.NotFound => Results.NotFound(),
        EmployeeChangeStatus.Invalid => Results.BadRequest(), EmployeeChangeStatus.Forbidden => Results.Forbid(),
        EmployeeChangeStatus.Unauthorized => Results.Unauthorized(), _ => Results.Conflict()
    };
}
