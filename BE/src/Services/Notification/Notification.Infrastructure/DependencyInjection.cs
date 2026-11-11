using MedEquipSLA.Application;
using MedEquipSLA.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using MedEquipSLA.Notification.Application.Abstractions;
using MedEquipSLA.Notification.Infrastructure.Persistence;
using MedEquipSLA.Notification.Infrastructure.Messaging;
namespace MedEquipSLA.Notification.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<Realtime.NotificationConnections>();
        services.AddScoped<IEmailDeliveryReader, EmailDeliveryReader>();
        services.AddDbContext<NotificationDbContext>(o => o.UseSqlServer(config.GetConnectionString("Database"), sql => sql.UseCompatibilityLevel(160)));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<NotificationDbContext>());
        services.AddScoped<IPersonalNotificationQueries, PersonalNotificationStore>();
        services.AddScoped<IPersonalNotificationCommands, PersonalNotificationStore>();
        services.AddScoped<INotificationPreferences, NotificationPreferences>();
        services.AddScoped<IEmailDeliveryQueue, EmailDeliveryQueue>();
        services.AddSingleton<EmailQueueCursor>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddHttpClient<IRecipientEmailDirectory, RecipientEmailDirectory>(client =>
        { client.BaseAddress = new Uri(config["Identity:Address"] ?? "http://localhost:5101"); client.Timeout = TimeSpan.FromSeconds(3); })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHostedService<Workers.EmailDeliveryWorker>();
        services.AddSignalR(options => options.MaximumReceiveMessageSize = 8192);
        services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, Realtime.NotificationUserIdProvider>();
        services.AddHostedService<Workers.NotificationHintWorker>();
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(config.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false"));
        services.AddScoped<IEventPublisher, RedisEventPublisher>();

        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<INotificationRecipientLookup, ReminderRecipientLookup>(client =>
        {
            client.BaseAddress = new Uri(config["Identity:Address"] ?? "http://localhost:5101");
            client.Timeout = TimeSpan.FromSeconds(3);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHostedService<Workers.NotificationConsumer>();
        return services;
    }
}
