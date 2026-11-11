using MedEquipSLA.Hosting;
using MedEquipSLA.Maintenance.Api;
using Grpc.Core;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
using MedEquipSLA.Maintenance.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddServiceScaffold(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentitySessionValidation(builder.Configuration);

var app = builder.Build();
app.UseServiceScaffold("Maintenance");
app.MapOperationalReports();
app.MapSourceOperations();
app.MapGet("/api/maintenance/equipment/{id:guid}", async (Guid id, HttpContext http,
    EquipmentLookup.EquipmentLookupClient client, CancellationToken ct) =>
{
    try
    {
        var response = await client.GetEquipmentAsync(new GetEquipmentRequest { EquipmentId = id.ToString() },
            new Metadata { { "authorization", http.Request.Headers.Authorization.ToString() } },
            deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: ct);
        return Results.Ok(response);
    }
    catch (RpcException ex) when (!ct.IsCancellationRequested)
    {
        return ex.StatusCode switch
        {
            StatusCode.InvalidArgument => Results.BadRequest(),
            StatusCode.NotFound => Results.NotFound(),
            StatusCode.PermissionDenied => Results.Forbid(),
            StatusCode.Unauthenticated => Results.Unauthorized(),
            _ => Results.Problem(statusCode: 503, title: "Equipment lookup is temporarily unavailable.")
        };
    }
}).RequireAuthorization();
app.MapWorkOrders();
app.MapWorkOrderOperations();
app.MapWorkOrderWorkflow();
app.MapScheduleQueries();
if (app.Environment.IsDevelopment()) app.MapGet("/api/diagnostics/equipment-grpc", async (MedEquipSLA.Contracts.Grpc.Equipment.V1.EquipmentLookup.EquipmentLookupClient client, CancellationToken ct) =>
{
    var reply = await client.GetServiceInfoAsync(new MedEquipSLA.Contracts.Grpc.Equipment.V1.ServiceInfoRequest(), deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: ct);
    return Results.Ok(new { reply.Service, reply.Phase, businessLookupImplemented = false });
}).AllowAnonymous();
await app.ApplyDevelopmentMigrationsAsync("Maintenance", ct =>
    MedEquipSLA.Maintenance.Infrastructure.Persistence.MaintenanceDatabaseMigration.ApplyAsync(app.Configuration, ct));
app.Run();
public partial class Program { }
