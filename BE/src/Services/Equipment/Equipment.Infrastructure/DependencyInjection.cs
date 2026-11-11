using MedEquipSLA.Application;
using MedEquipSLA.Equipment.Application.Features.Lookup;
using MedEquipSLA.Equipment.Application.Features.Departments;
using MedEquipSLA.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using MedEquipSLA.Equipment.Infrastructure.Persistence;
using MedEquipSLA.Equipment.Infrastructure.Messaging;
namespace MedEquipSLA.Equipment.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.IReconciliationReader,ReconciliationReader>();
        services.AddScoped<MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.ReconciliationQuery>();
        services.AddScoped<MedEquipSLA.Equipment.Application.Features.Reports.IOperationalReportReader, OperationalReportReader>();
        services.AddScoped<MedEquipSLA.Equipment.Application.Features.Reports.OperationalReportQuery>();
        services.AddDbContext<EquipmentDbContext>(o => o.UseSqlServer(config.GetConnectionString("Database"), sql => sql.UseCompatibilityLevel(160)));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<EquipmentDbContext>());
        services.AddScoped<MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.IUserResourceRegistrationStore, UserResourceRegistrationStore>();
        services.AddScoped<MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.UserResourceRegistrationHandler>();
        services.AddScoped<MedEquipSLA.Equipment.Application.Features.Retirement.IRetirementStore, RetirementStore>();
        services.AddScoped<MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.IResourceRegistrationStore, ResourceRegistrationStore>();
        services.AddScoped<MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.IWorkOrderRegistrationStore, WorkOrderRegistrationStore>();
        services.AddScoped<MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.WorkOrderRegistrationHandler>();
        services.AddScoped<MedEquipSLA.Equipment.Application.Features.Assets.IAssetCatalog, AssetCatalogStore>();
        services.AddScoped<IDepartmentStore, DepartmentStore>();
        services.AddScoped<IEquipmentLookupReader, EquipmentQueryAdapter>();
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(config.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false"));
        services.AddScoped<IEventPublisher, RedisEventPublisher>();
        services.AddHostedService<Workers.IncidentLifecycleConsumer>();
        services.AddHostedService<Workers.WorkOrderLifecycleConsumer>();
        services.AddHostedService<Workers.RegistrationSnapshotConsumer>();
        services.AddHostedService<Workers.EquipmentOutboxDispatcher>();


        return services;
    }
}
