using MedEquipSLA.Hosting;
using MedEquipSLA.Incident.Api;
using MedEquipSLA.Incident.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddServiceScaffold(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentitySessionValidation(builder.Configuration);

var app = builder.Build();
app.UseServiceScaffold("Incident");
app.MapOperationalReports();
app.MapSourceOperations();
app.MapRepairEvidence();
app.MapIncidentWorkflow();
app.MapIncidentCreation();
app.MapIncidentQueries();
app.MapSlaPolicies();
if (app.Environment.IsDevelopment()) app.MapGet("/api/diagnostics/equipment-grpc", async (MedEquipSLA.Contracts.Grpc.Equipment.V1.EquipmentLookup.EquipmentLookupClient client, CancellationToken ct) =>
{
    var reply = await client.GetServiceInfoAsync(new MedEquipSLA.Contracts.Grpc.Equipment.V1.ServiceInfoRequest(), deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: ct);
    return Results.Ok(new { reply.Service, reply.Phase, businessLookupImplemented = false });
}).AllowAnonymous();
await app.ApplyDevelopmentMigrationsAsync("Incident", ct =>
    MedEquipSLA.Incident.Infrastructure.Persistence.IncidentDatabaseMigration.ApplyAsync(app.Configuration, ct));
app.Run();
public partial class Program { }
