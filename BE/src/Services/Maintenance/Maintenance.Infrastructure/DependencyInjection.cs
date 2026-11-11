using MedEquipSLA.Application;
using MedEquipSLA.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using MedEquipSLA.Maintenance.Infrastructure.Persistence;
using MedEquipSLA.Maintenance.Infrastructure.Messaging;
namespace MedEquipSLA.Maintenance.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddHostedService<Workers.RegistrationSnapshotExporter>();
        services.AddScoped<MedEquipSLA.Maintenance.Application.Features.WorkOrders.WorkOrderAssignmentCommand>();
        services.AddHttpClient<MedEquipSLA.Maintenance.Application.Features.WorkOrders.ITechnicianEligibility, Messaging.TechnicianEligibility>(client =>
        { client.BaseAddress = new Uri(config["Identity:Address"] ?? "http://localhost:5101"); client.Timeout = TimeSpan.FromSeconds(3); })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<MedEquipSLA.Maintenance.Application.Features.Reports.IOperationalReportReader, OperationalReportReader>();
        services.AddScoped<MedEquipSLA.Maintenance.Application.Features.Reports.OperationalReportQuery>();
        services.AddHostedService<Workers.UserAccessConsumer>();
        services.AddDbContext<MaintenanceDbContext>(o => o.UseSqlServer(config.GetConnectionString("Database"), sql => sql.UseCompatibilityLevel(160)));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<MaintenanceDbContext>());
        services.AddScoped<MedEquipSLA.Maintenance.Application.Features.WorkOrders.IWorkOrderReader, WorkOrderReader>();
        services.AddScoped<MedEquipSLA.Maintenance.Application.Features.WorkOrders.IWorkOrderAssignment, WorkOrderAssignment>();
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(config.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false"));
        services.AddScoped<IEventPublisher, RedisEventPublisher>();
        services.AddGrpcClient<MedEquipSLA.Contracts.Grpc.Equipment.V1.EquipmentLookup.EquipmentLookupClient>(o => o.Address = new Uri(config["Grpc:EquipmentAddress"] ?? "http://localhost:5103"));
        services.AddScoped<MedEquipSLA.Maintenance.Application.Features.Schedules.IScheduleReader, ScheduleReader>();
        services.AddScoped<MedEquipSLA.Maintenance.Application.Features.Schedules.IScheduleCommands, ScheduleCommands>();
        services.AddScoped<MedEquipSLA.Maintenance.Application.Features.Schedules.IScheduleTermination, ScheduleTermination>();
        services.AddSingleton<ScheduleReleaseCursor>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<MedEquipSLA.Application.Coordination.ISourceRetirementGate, SourceRetirementGate>();
        services.AddScoped<MedEquipSLA.Application.Coordination.ISourceCreationOperations, SourceCreationOperations>();
        services.AddHostedService<Workers.SourceCancellationWorker>();
        services.AddScoped<Workers.RetirementSourceCoordinator>();
        services.AddScoped<MedEquipSLA.Maintenance.Application.Features.WorkOrders.IWorkOrderGeneration, WorkOrderGeneration>();
        services.AddScoped<MedEquipSLA.Maintenance.Application.Features.WorkOrders.IWorkOrderOperationAdmin, WorkOrderOperationAdmin>();
        services.AddScoped<MedEquipSLA.Maintenance.Application.Features.WorkOrders.IWorkOrderWorkflow, WorkOrderWorkflow>();
        services.AddHostedService<Workers.WorkOrderGenerationWorker>();
        services.AddHostedService<Workers.RetirementSourceWorker>();
        services.AddSingleton<OccurrencePlanningCursor>();
        services.AddOptions<OccurrencePlanningOptions>().Bind(config.GetSection("Workers:OccurrencePlanning"))
            .Validate(x => x.ScheduleBatchSize is >= 1 and <= 100 && x.OccurrencesPerSchedule is >= 1 and <= 16,
                "Occurrence planning limits must be 1..100 schedules and 1..16 occurrences per schedule.")
            .ValidateOnStart();
        services.AddHttpClient<INotificationRecipientLookup, ReminderRecipientLookup>(client =>
        {
            client.BaseAddress = new Uri(config["Identity:Address"] ?? "http://localhost:5101");
            client.Timeout = TimeSpan.FromSeconds(3);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<ReminderPlanner>();
        services.AddHostedService<Workers.MaintenanceReminderWorker>();
        services.AddScoped<OccurrencePlanner>();
        services.AddHostedService<Workers.MaintenanceScheduler>();
        services.AddHostedService<Workers.ScheduleOutboxDispatcher>();
        return services;
    }
}
