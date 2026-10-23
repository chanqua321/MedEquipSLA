using MedEquipSLA.Hosting;
using MedEquipSLA.Notification.Infrastructure;
using FluentValidation;
using MedEquipSLA.Notification.Application.Features.ScaffoldCommand;
using MedEquipSLA.Notification.Application.Features.ScaffoldQuery;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddServiceScaffold(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<ScaffoldCommandValidator>();
builder.Services.AddScoped<ScaffoldCommandHandler>();
builder.Services.AddScoped<ScaffoldQueryHandler>();

var app = builder.Build();
app.UseServiceScaffold("Notification");
app.MapGet("/api/notifications", async (ScaffoldQueryHandler handler, CancellationToken ct) => Results.Ok(await handler.Handle(new ScaffoldQuery(Guid.Empty), ct))).RequireAuthorization();

app.Run();
public partial class Program { }
