using MedEquipSLA.Maintenance.Application.Features.WorkOrders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedEquipSLA.Maintenance.Infrastructure.Workers;

public sealed class WorkOrderGenerationWorker(IServiceScopeFactory scopes, IConfiguration config,
    ILogger<WorkOrderGenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue<bool>("Workers:Enabled") || !config.GetValue<bool>("Workers:WorkOrderGeneration:Enabled")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IWorkOrderGeneration>().RunBatchAsync(ct);
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            { logger.LogWarning(error, "WorkOrder generation tick failed; durable pending operations are preserved."); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
