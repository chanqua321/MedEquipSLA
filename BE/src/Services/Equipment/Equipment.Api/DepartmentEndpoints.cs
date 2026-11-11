using System.Security.Claims;
using MedEquipSLA.Equipment.Application.Features.Departments;

namespace MedEquipSLA.Equipment.Api;

internal static class DepartmentEndpoints
{
    public static void MapDepartments(this WebApplication app)
    {
        var routes = app.MapGroup("/api/departments").WithTags("Departments")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        routes.MapGet("/", async (string? search, bool? active, string? sort, int? page, int? pageSize,
            DepartmentQueries queries, CancellationToken ct) =>
        {
            var query = new DepartmentQuery(search?.Trim(), active, sort ?? "name", page ?? 1, pageSize ?? 20);
            if (query.Page is < 1 or > 1_000_000 || query.PageSize is < 1 or > 100 ||
                query.Search?.Length > 200 || query.Sort is not ("name" or "-name" or "code" or "-code"))
                return Results.Problem(statusCode: 400, title: "Invalid search, sort or pagination.");
            return Results.Ok(await queries.ListAsync(query, ct));
        });

        routes.MapGet("/{id:guid}", async (Guid id, DepartmentQueries queries, CancellationToken ct) =>
            await queries.FindAsync(id, ct) is { } result ? Results.Ok(result) : Results.NotFound());

        routes.MapPost("/", async (CreateDepartment input, HttpContext http, DepartmentCommands commands, CancellationToken ct) =>
        {
            if (!TryActor(http, out var actor)) return Results.Forbid();
            var code = input.Code?.Trim();
            var name = input.Name?.Trim();
            if (code is not { Length: > 0 and <= 50 } || name is not { Length: > 0 and <= 200 })
                return Results.Problem(statusCode: 400, title: "Code and name are required and must fit the field limits.");
            var result = await commands.CreateAsync(code, name, actor, http.TraceIdentifier, ct);
            return result.Department is { } department
                ? Results.Created($"/api/departments/{department.Id}", department)
                : WriteResponse(result);
        });

        routes.MapPut("/{id:guid}", async (Guid id, UpdateDepartment input, HttpContext http, DepartmentCommands commands, CancellationToken ct) =>
        {
            if (!TryActor(http, out var actor)) return Results.Forbid();
            var name = input.Name?.Trim();
            if (name is not { Length: > 0 and <= 200 } || input.RowVersion is not { Length: 8 })
                return Results.Problem(statusCode: 400, title: "Valid name and SQL rowversion are required.");
            return WriteResponse(await commands.UpdateAsync(id, input.RowVersion, name, actor, http.TraceIdentifier, ct));
        });

        routes.MapDelete("/{id:guid}", async (Guid id, string? version, HttpContext http, DepartmentCommands commands, CancellationToken ct) =>
        {
            if (!TryActor(http, out var actor)) return Results.Forbid();
            var bytes = new byte[8];
            if (version is null || !Convert.TryFromBase64String(version, bytes, out var written) || written != bytes.Length)
                return Results.Problem(statusCode: 400, title: "The version query parameter must be an eight-byte base64 rowversion.");
            var result = await commands.ArchiveAsync(id, bytes, actor, http.TraceIdentifier, ct);
            return result.Status == DepartmentWriteStatus.Saved ? Results.NoContent() : WriteResponse(result);
        });
    }

    private static bool TryActor(HttpContext http, out Guid actor) =>
        Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);

    private static IResult WriteResponse(DepartmentWrite result) => result.Status switch
    {
        DepartmentWriteStatus.Saved => Results.Ok(result.Department),
        DepartmentWriteStatus.NotFound => Results.NotFound(),
        _ => Results.Problem(statusCode: 409, title: "Department code already exists or the row version is stale.")
    };

    internal sealed record CreateDepartment(string? Code, string? Name);
    internal sealed record UpdateDepartment(string? Name, byte[]? RowVersion);
}
