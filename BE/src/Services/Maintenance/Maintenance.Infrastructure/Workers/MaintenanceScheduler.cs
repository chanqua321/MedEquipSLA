using MedEquipSLA.Maintenance.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedEquipSLA.Maintenance.Infrastructure.Workers;

public sealed class MaintenanceScheduler(IConfiguration config, IServiceScopeFactory scopes,
    TimeProvider timeProvider, ILogger<MaintenanceScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue<bool>("Workers:Enabled")) return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), timeProvider);
        do
        {
            var started = timeProvider.GetTimestamp();
            logger.LogInformation("Maintenance scheduler tick started.");
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var planner = scope.ServiceProvider.GetRequiredService<OccurrencePlanner>();
                var planned = await planner.PlanBatchAsync(timeProvider.GetUtcNow(), stoppingToken);
                if (planned > 0) logger.LogInformation("Planned {OccurrenceCount} maintenance occurrences.", planned);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Maintenance planning failed. Eligible schedules will be retried.");
            }
            finally
            {
                logger.LogInformation("Maintenance scheduler tick ended after {ElapsedMilliseconds} ms.",
                    timeProvider.GetElapsedTime(started).TotalMilliseconds);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
