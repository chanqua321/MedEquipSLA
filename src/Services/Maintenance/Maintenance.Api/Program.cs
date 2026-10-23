using MedEquipSLA.Hosting;
using MedEquipSLA.Maintenance.Infrastructure;
using FluentValidation;
using MedEquipSLA.Maintenance.Application.Features.ScaffoldCommand;
using MedEquipSLA.Maintenance.Application.Features.ScaffoldQuery;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddServiceScaffold(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<ScaffoldCommandValidator>();
builder.Services.AddScoped<ScaffoldCommandHandler>();
builder.Services.AddScoped<ScaffoldQueryHandler>();

var app = builder.Build();
app.UseServiceScaffold("Maintenance");
app.MapGet("/api/work-orders", async (ScaffoldQueryHandler handler, CancellationToken ct) => Results.Ok(await handler.Handle(new ScaffoldQuery(Guid.Empty), ct))).RequireAuthorization();
if (app.Environment.IsDevelopment()) app.MapGet("/api/diagnostics/equipment-grpc", async (MedEquipSLA.Contracts.Grpc.Equipment.V1.EquipmentLookup.EquipmentLookupClient client, CancellationToken ct) =>
{
    var reply = await client.GetServiceInfoAsync(new MedEquipSLA.Contracts.Grpc.Equipment.V1.ServiceInfoRequest(), deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: ct);
    return Results.Ok(new { reply.Service, reply.Phase, businessLookupImplemented = false });
}).AllowAnonymous();
app.Run();
public partial class Program { }
