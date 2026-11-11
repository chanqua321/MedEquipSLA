using MedEquipSLA.Application;
using MedEquipSLA.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using MedEquipSLA.Incident.Infrastructure.Persistence;
using MedEquipSLA.Incident.Infrastructure.Messaging;
namespace MedEquipSLA.Incident.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddHostedService<Workers.RegistrationSnapshotExporter>();
        services.AddScoped<MedEquipSLA.Incident.Application.Features.Incidents.IIncidentRepairStore, IncidentRepairStore>();
        services.AddScoped<MedEquipSLA.Incident.Application.Features.Incidents.IncidentRepairCommands>();
        services.AddScoped<MedEquipSLA.Incident.Application.Features.Incidents.IncidentRepairQueries>();
        services.AddScoped<MedEquipSLA.Incident.Application.Features.Incidents.IIncidentWorkflowStore, IncidentWorkflowStore>();
        services.AddScoped<MedEquipSLA.Incident.Application.Features.Incidents.IncidentWorkflowCommands>();
        services.AddHttpClient<MedEquipSLA.Incident.Application.Features.Incidents.ITechnicianEligibility, TechnicianEligibility>(client =>
        { client.BaseAddress = new Uri(config["Identity:Address"] ?? "http://localhost:5101"); client.Timeout = TimeSpan.FromSeconds(3); })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<MedEquipSLA.Incident.Application.Features.Sla.ISlaPolicyStore, SlaPolicyStore>();
        services.AddScoped<MedEquipSLA.Incident.Application.Features.Sla.SlaPolicyCommands>();
        services.AddScoped<MedEquipSLA.Incident.Application.Features.Sla.SlaPolicyQueries>();
        services.AddScoped<MedEquipSLA.Incident.Application.Features.Incidents.IIncidentCreationStore, IncidentCreationStore>();
        services.AddScoped<MedEquipSLA.Incident.Application.Features.Incidents.IIncidentEquipmentRegistration, IncidentEquipmentRegistration>();
        services.AddScoped<MedEquipSLA.Incident.Application.Features.Incidents.CreateIncidentHandler>();
        services.AddScoped<MedEquipSLA.Incident.Application.Features.Reports.IOperationalReportReader, OperationalReportReader>();
        services.AddScoped<MedEquipSLA.Incident.Application.Features.Reports.OperationalReportQuery>();
        services.AddHostedService<Workers.UserAccessConsumer>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<MedEquipSLA.Application.Coordination.ISourceRetirementGate, SourceRetirementGate>();
        services.AddScoped<MedEquipSLA.Application.Coordination.ISourceCreationOperations, SourceCreationOperations>();
        services.AddHostedService<Workers.SourceCancellationWorker>();
        services.AddScoped<Workers.RetirementSourceCoordinator>();
        services.AddHostedService<Workers.RetirementSourceWorker>();
        services.AddDbContext<IncidentDbContext>(o => o.UseSqlServer(config.GetConnectionString("Database"), sql => sql.UseCompatibilityLevel(160)));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IncidentDbContext>());
        services.AddScoped<MedEquipSLA.Incident.Application.Features.Incidents.IIncidentReader, IncidentReader>();
        services.AddScoped<MedEquipSLA.Incident.Application.Features.Incidents.IncidentQueries>();
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(config.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false"));
        services.AddScoped<IEventPublisher, RedisEventPublisher>();
        services.AddGrpcClient<MedEquipSLA.Contracts.Grpc.Equipment.V1.EquipmentLookup.EquipmentLookupClient>(o => o.Address = new Uri(config["Grpc:EquipmentAddress"] ?? "http://localhost:5103"));
        services.AddHostedService<Workers.SlaDeadlineScanner>();
        services.AddHostedService<Workers.SlaOutboxDispatcher>();
        return services;
    }
}
