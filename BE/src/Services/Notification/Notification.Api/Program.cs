using MedEquipSLA.Hosting;
using MedEquipSLA.Notification.Api;
using MedEquipSLA.Notification.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddServiceScaffold(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentitySessionValidation(builder.Configuration);
builder.Services.AddHubQueryTokenSupport("/hubs/notifications");

var app = builder.Build();
app.UseServiceScaffold("Notification");
app.MapPersonalNotifications();
app.MapNotificationSettings();
app.MapHub<MedEquipSLA.Notification.Infrastructure.Realtime.NotificationHub>("/hubs/notifications", options =>
    options.CloseOnAuthenticationExpiration = true).RequireAuthorization();

await app.ApplyDevelopmentMigrationsAsync("Notification", ct =>
    MedEquipSLA.Notification.Infrastructure.Persistence.NotificationDatabaseMigration.ApplyAsync(app.Configuration, ct));
app.Run();
public partial class Program { }
