using MedEquipSLA.Application;
using MedEquipSLA.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using MedEquipSLA.Incident.Application.Abstractions;
using MedEquipSLA.Incident.Infrastructure.Persistence;
using MedEquipSLA.Incident.Infrastructure.Messaging;
namespace MedEquipSLA.Incident.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<IncidentDbContext>(o => o.UseSqlServer(config.GetConnectionString("Database"), sql => sql.UseCompatibilityLevel(160)));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IncidentDbContext>());
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IIncidentQueryAdapter, IncidentQueryAdapter>();
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(config.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false"));
        services.AddScoped<IEventPublisher, RedisEventPublisher>();
        services.AddGrpcClient<MedEquipSLA.Contracts.Grpc.Equipment.V1.EquipmentLookup.EquipmentLookupClient>(o => o.Address = new Uri(config["Grpc:EquipmentAddress"] ?? "http://localhost:5103"));
        services.AddHostedService<Workers.SlaDeadlineScanner>();
        return services;
    }
}
