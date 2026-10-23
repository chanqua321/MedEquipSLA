using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace MedEquipSLA.Maintenance.Infrastructure.Workers;
public sealed class MaintenanceScheduler(IConfiguration config, ILogger<MaintenanceScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue<bool>("Workers:Enabled"))
        { logger.LogInformation("MaintenanceScheduler disabled in scaffold; no business work executed"); return; }
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try { while (await timer.WaitForNextTickAsync(stoppingToken))
            logger.LogInformation("MaintenanceScheduler scaffold tick only. No DB scan, consumer, or business event is implemented"); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
