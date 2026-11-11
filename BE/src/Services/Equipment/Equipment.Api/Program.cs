using MedEquipSLA.Hosting;
using MedEquipSLA.Equipment.Api;
using MedEquipSLA.Equipment.Api.Authentication;
using MedEquipSLA.Equipment.Application.Features.Lookup;
using MedEquipSLA.Equipment.Application.Features.Departments;
using MedEquipSLA.Equipment.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddServiceScaffold(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGrpc();
builder.Services.AddEquipmentServiceAuthentication();
builder.Services.AddIdentitySessionValidation(builder.Configuration);
builder.Services.AddScoped<EquipmentLookupQuery>();
builder.Services.AddScoped<DepartmentCommands>();
builder.Services.AddScoped<DepartmentQueries>();
var app = builder.Build();
app.UseServiceScaffold("Equipment");
app.MapOperationalReports();
app.MapGet("/api/equipment", async (string? search, int? page, HttpContext http, EquipmentLookupQuery query, CancellationToken ct) =>
{
    if (!EquipmentAccess.TryScope(http.User, out var department)) return Results.Forbid();
    if (search?.Length > 200 || page is < 1 or > 1000000) return Results.BadRequest();
    return Results.Ok(await query.ListAsync(department, search?.Trim(), page ?? 1, ct));
}).RequireAuthorization().WithTags("Equipment");
app.MapGet("/api/equipment/{id:guid}", async (Guid id, HttpContext http, EquipmentLookupQuery query, CancellationToken ct) =>
{
    if (!EquipmentAccess.TryScope(http.User, out var department)) return Results.Forbid();
    var asset = await query.Handle(id, department, ct);
    return asset is null ? Results.NotFound() : Results.Ok(asset);
}).RequireAuthorization();
app.MapDepartments();
app.MapAssetCatalog();
app.MapRetirement();
app.MapGet("/api/equipment-reconciliation", async (MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.ReconciliationQuery query, CancellationToken ct) =>
    Results.Ok(await query.ExecuteAsync(ct))).RequireAuthorization(policy => policy.RequireRole("Admin")).WithTags("Equipment reconciliation");
app.MapGrpcService<MedEquipSLA.Equipment.Api.Grpc.EquipmentGrpcService>();
await app.ApplyDevelopmentMigrationsAsync("Equipment", ct =>
    MedEquipSLA.Equipment.Infrastructure.Persistence.EquipmentDatabaseMigration.ApplyAsync(app.Configuration, ct));
app.Run();
public partial class Program { }
