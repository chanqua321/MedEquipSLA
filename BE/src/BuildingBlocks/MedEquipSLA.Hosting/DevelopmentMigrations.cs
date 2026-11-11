using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedEquipSLA.Hosting;

public static class DevelopmentMigrations
{
    // The API supplies its own service-owned EF migration callback. Hosting stays
    // independent of EF Core, SqlClient and the five business DbContexts.
    public static async Task ApplyDevelopmentMigrationsAsync(this WebApplication app,
        string service, Func<CancellationToken, Task> migrate)
    {
        if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("Migrations:ApplyOnStartup"))
            return;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(app.Lifetime.ApplicationStopping);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        app.Logger.LogInformation("Applying pending development migrations for {Service} before API startup", service);
        try
        {
            await migrate(timeout.Token);
            app.Logger.LogInformation("Development database ready for {Service}; migrations are up to date", service);
        }
        catch (Exception exception)
        {
            // Connection strings can contain credentials; don't include exception
            // messages or inner exceptions in the startup failure report.
            app.Logger.LogError("Development migration failed for {Service}. Failure type: {FailureType}. API startup stopped.",
                service, exception.GetType().Name);
            throw new InvalidOperationException($"Development migration failed for {service}. Check SQL Server, database ownership and migration permissions; use the EF CLI for diagnostics.");
        }
    }
}
