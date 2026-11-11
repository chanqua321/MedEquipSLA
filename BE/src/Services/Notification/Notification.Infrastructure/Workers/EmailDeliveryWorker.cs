using MedEquipSLA.Notification.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedEquipSLA.Notification.Infrastructure.Workers;

public sealed class EmailDeliveryWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<EmailDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue<bool>("Workers:Enabled") || !config.GetValue<bool>("Email:Enabled")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var queue = scope.ServiceProvider.GetRequiredService<IEmailDeliveryQueue>();
                await queue.QueueBatchAsync(ct); await queue.SendBatchAsync(ct);
            }
            catch (Exception failure) when (!ct.IsCancellationRequested)
            { logger.LogWarning(failure, "Email delivery tick failed; durable queue and attempt history are retained."); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
