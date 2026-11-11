using MedEquipSLA.Application;
using Microsoft.AspNetCore.Identity;
using MedEquipSLA.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using MedEquipSLA.Identity.Application.Abstractions;
using MedEquipSLA.Identity.Infrastructure.Persistence;
using MedEquipSLA.Identity.Infrastructure.Messaging;
using Microsoft.AspNetCore.DataProtection;
namespace MedEquipSLA.Identity.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IEmployeeStore, EmployeeStore>();
        services.AddScoped<IAdminProvisioning, AdminProvisioning>();
        services.AddScoped<IIdentityRecipientDirectory, IdentityRecipientDirectory>();
        services.AddScoped<EmployeeCommands>();
        services.AddScoped<EmployeeQueries>();
        services.AddDbContext<IdentityDbContext>(o => o.UseSqlServer(config.GetConnectionString("Database"), sql => sql.UseCompatibilityLevel(160)));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<IdentityDbContext>();
        services.AddScoped<IIdentitySessions, IdentitySessions>();
        var dataProtection = services.AddDataProtection().SetApplicationName("MedEquipSLA.Identity");
        if (config["Identity:DataProtectionPath"] is { Length: > 0 } keyPath)
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        services.AddScoped<IPasswordValidator<ApplicationUser>, CommonPasswordValidator>();
        services.AddScoped<SecurityTickets>();
        services.AddScoped<MfaSecurity>();
        services.AddScoped<IMfaSecurity>(sp => sp.GetRequiredService<MfaSecurity>());
        services.AddScoped<IAccountProvisioning, AccountProvisioning>();
        services.AddHttpClient("EquipmentDirectory", client =>
        { client.BaseAddress = new Uri(config["Equipment:Address"] ?? "http://localhost:5102"); client.Timeout = TimeSpan.FromSeconds(3); })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHostedService<Workers.IdentityOutboxDispatcher>();
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(config.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false"));
        services.AddScoped<IEventPublisher, RedisEventPublisher>();


        return services;
    }
}
