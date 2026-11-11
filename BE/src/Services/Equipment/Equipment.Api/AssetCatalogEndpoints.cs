using System.Security.Claims;
using MedEquipSLA.Equipment.Application.Features.Assets;
using MedEquipSLA.Equipment.Domain;

namespace MedEquipSLA.Equipment.Api;

internal static class AssetCatalogEndpoints
{
    public static void MapAssetCatalog(this WebApplication app)
    {
        var assets = app.MapGroup("/api/equipment").WithTags("Equipment catalog")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));
        assets.MapGet("/{id:guid}/profile", async (Guid id, IAssetCatalog catalog, CancellationToken ct) =>
            await catalog.FindAsync(id, ct) is { } item ? Results.Ok(item) : Results.NotFound());
        assets.MapPost("", async (CreateAsset input, HttpContext http, IAssetCatalog catalog, CancellationToken ct) =>
        {
            if (!Actor(http, out var actor)) return Results.Forbid();
            var code = input.Code?.Trim();
            var profile = Normalize(input.Profile);
            if (code is not { Length: > 0 and <= 50 } || input.DepartmentId == Guid.Empty || profile is null)
                return Results.BadRequest();
            var result = await catalog.SaveAssetAsync(null, code, input.DepartmentId, null, profile, actor, http.TraceIdentifier, ct);
            return result.Value is { } asset ? Results.Created($"/api/equipment/{asset.Id}/profile", asset) : Failure(result.Status);
        });
        assets.MapPut("/{id:guid}/profile", async (Guid id, UpdateAsset input, HttpContext http, IAssetCatalog catalog, CancellationToken ct) =>
        {
            if (!Actor(http, out var actor)) return Results.Forbid();
            var profile = Normalize(input.Profile);
            if (input.RowVersion is not { Length: 8 } || profile is null) return Results.BadRequest();
            var result = await catalog.SaveAssetAsync(id, null, null, input.RowVersion, profile, actor, http.TraceIdentifier, ct);
            return result.Value is { } asset ? Results.Ok(asset) : Failure(result.Status);
        });

        assets.MapPost("/{id:guid}/department-transfer", async (Guid id, TransferAsset input, HttpContext http,
            IAssetCatalog catalog, CancellationToken ct) =>
        {
            if (!Actor(http, out var actor)) return Results.Forbid();
            var reason = input.Reason?.Trim();
            if (input.DepartmentId == Guid.Empty || input.RowVersion is not { Length: 8 } ||
                reason is not { Length: > 0 and <= 2000 }) return Results.BadRequest();
            var result = await catalog.TransferAsync(id, input.DepartmentId, input.RowVersion, reason, actor, http.TraceIdentifier, ct);
            return result.Value is { } asset ? Results.Ok(asset) : Failure(result.Status);
        });

        var categories = app.MapGroup("/api/equipment-categories").WithTags("Equipment catalog")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));
        categories.MapGet("", async (string? search, int? page, IAssetCatalog catalog, CancellationToken ct) =>
        {
            if (search?.Length > 200 || page is < 1 or > 1_000_000) return Results.BadRequest();
            return Results.Ok(await catalog.CategoriesAsync(search?.Trim(), page ?? 1, ct));
        });
        categories.MapPost("", async (CreateCategory input, HttpContext http, IAssetCatalog catalog, CancellationToken ct) =>
        {
            if (!Actor(http, out var actor)) return Results.Forbid();
            var code = input.Code?.Trim();
            var name = input.Name?.Trim();
            if (code is not { Length: > 0 and <= 50 } || name is not { Length: > 0 and <= 200 }) return Results.BadRequest();
            var result = await catalog.CreateCategoryAsync(code, name, actor, http.TraceIdentifier, ct);
            return result.Value is { } category ? Results.Json(category, statusCode: 201) : Failure(result.Status);
        });
        categories.MapGet("/{id:guid}", async (Guid id, IAssetCatalog catalog, CancellationToken ct) =>
            await catalog.FindCategoryAsync(id, ct) is { } row ? Results.Ok(row) : Results.NotFound());
        categories.MapPut("/{id:guid}", async (Guid id, UpdateCategory input, HttpContext http, IAssetCatalog catalog, CancellationToken ct) =>
        {
            if (!Actor(http, out var actor)) return Results.Forbid();
            if (input.Name?.Trim().Length is not (> 0 and <= 200) || input.Description?.Length > 1000 || input.RowVersion is not { Length: 8 })
                return Results.BadRequest();
            var result = await catalog.UpdateCategoryAsync(id, input.Name.Trim(), input.Description?.Trim(), input.Active,
                input.RowVersion, actor, http.TraceIdentifier, ct);
            return result.Value is { } row ? Results.Ok(row) : Failure(result.Status);
        });
        categories.MapDelete("/{id:guid}", async (Guid id, string? version, HttpContext http, IAssetCatalog catalog, CancellationToken ct) =>
        {
            if (!Actor(http, out var actor)) return Results.Forbid();
            var bytes = new byte[8];
            if (version is null || !Convert.TryFromBase64String(version, bytes, out var written) || written != 8) return Results.BadRequest();
            var row = await catalog.FindCategoryAsync(id, ct);
            if (row is null) return Results.NotFound();
            var result = await catalog.UpdateCategoryAsync(id, row.Name, row.Description, false, bytes, actor, http.TraceIdentifier, ct);
            return result.Status == CatalogStatus.Saved ? Results.NoContent() : Failure(result.Status);
        });
    }

    private static EquipmentProfile? Normalize(EquipmentProfile? profile)
    {
        if (profile is null) return null;
        var name = profile.Name?.Trim();
        var serial = Optional(profile.SerialNumber);
        var manufacturer = Optional(profile.Manufacturer);
        var model = Optional(profile.Model);
        if (name is not { Length: > 0 and <= 200 } || profile.CategoryId == Guid.Empty ||
            serial?.Length > 100 || manufacturer?.Length > 200 || model?.Length > 100 ||
            profile.WarrantyUntil < profile.PurchasedOn)
            return null;
        return profile with { Name = name, SerialNumber = serial, Manufacturer = manufacturer, Model = model };
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool Actor(HttpContext http, out Guid actor) =>
        Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out actor) && actor != Guid.Empty;

    private static IResult Failure(CatalogStatus status) => status switch
    {
        CatalogStatus.NotFound => Results.NotFound(),
        CatalogStatus.InvalidReference => Results.Problem(statusCode: 400, title: "Select an active department and equipment category."),
        _ => Results.Conflict()
    };

    internal sealed record CreateAsset(string? Code, Guid DepartmentId, EquipmentProfile? Profile);
    internal sealed record UpdateAsset(byte[]? RowVersion, EquipmentProfile? Profile);
    internal sealed record CreateCategory(string? Code, string? Name);
    internal sealed record UpdateCategory(string? Name, string? Description, bool Active, byte[]? RowVersion);
    internal sealed record TransferAsset(Guid DepartmentId, byte[]? RowVersion, string? Reason);
}
