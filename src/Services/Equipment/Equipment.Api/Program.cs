using MedEquipSLA.Hosting;
using MedEquipSLA.Equipment.Infrastructure;
using FluentValidation;
using MedEquipSLA.Equipment.Application.Features.ScaffoldCommand;
using MedEquipSLA.Equipment.Application.Features.ScaffoldQuery;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddServiceScaffold(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<ScaffoldCommandValidator>();
builder.Services.AddScoped<ScaffoldCommandHandler>();
builder.Services.AddScoped<ScaffoldQueryHandler>();
builder.Services.AddGrpc();
var app = builder.Build();
app.UseServiceScaffold("Equipment");
app.MapGet("/api/equipment", async (ScaffoldQueryHandler handler, CancellationToken ct) => Results.Ok(await handler.Handle(new ScaffoldQuery(Guid.Empty), ct))).RequireAuthorization();
app.MapGrpcService<MedEquipSLA.Equipment.Api.Grpc.EquipmentGrpcService>();
app.Run();
public partial class Program { }
