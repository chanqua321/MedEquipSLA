using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace MedEquipSLA.Notification.Infrastructure.Workers;
public sealed class NotificationConsumer(IConfiguration config, ILogger<NotificationConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue<bool>("Workers:Enabled"))
        { logger.LogInformation("NotificationConsumer disabled in scaffold; no business work executed"); return; }
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try { while (await timer.WaitForNextTickAsync(stoppingToken))
            logger.LogInformation("NotificationConsumer scaffold tick only. No DB scan, consumer, or business event is implemented"); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
