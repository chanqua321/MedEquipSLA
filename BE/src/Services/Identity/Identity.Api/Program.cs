using MedEquipSLA.Hosting;
using MedEquipSLA.Identity.Api;
using MedEquipSLA.Identity.Infrastructure;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        context.Request.Path.StartsWithSegments("/api/auth") && context.Request.Path != "/api/auth/me" && context.Request.Path != "/api/auth/logout"
            ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
            : RateLimitPartition.GetNoLimiter("other"));
});
builder.Services.AddServiceScaffold(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddSingleton<AccessTokenIssuer>();

var app = builder.Build();
app.UseRateLimiter();
app.UseServiceScaffold("Identity");
app.MapSessions();
// Public feature state only: FE uses the same flag as BE instead of maintaining a separate bypass switch.
app.MapGet("/api/auth/features", (HttpContext http) =>
{
    http.Response.Headers.CacheControl = "no-store";
    return Results.Ok(new { mfaEnabled = app.Configuration.GetValue(MedEquipSLA.Identity.Infrastructure.Persistence.MfaSecurity.ConfigurationKey, true) });
}).AllowAnonymous().WithTags("Authentication");
app.MapMfa();

app.MapEmployeeDirectory();
app.MapAccountProvisioning();
app.MapEmployeeImport();
app.MapNotificationRecipients();

await app.ApplyDevelopmentMigrationsAsync("Identity", ct =>
    MedEquipSLA.Identity.Infrastructure.Persistence.IdentityDatabaseMigration.ApplyAsync(app.Configuration, ct));
if (app.Configuration.GetValue<bool>("BootstrapAdmin:Run"))
{
    await AdminBootstrap.RunAsync(app.Services, app.Configuration);
    return;
}

_ = app.Services.GetRequiredService<AccessTokenIssuer>();
app.Run();
public partial class Program { }
