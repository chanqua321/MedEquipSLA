using MedEquipSLA.Application.Coordination;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedEquipSLA.Maintenance.Infrastructure.Workers;

public sealed class SourceCancellationWorker(IServiceScopeFactory scopes, IConfiguration config,
    ILogger<SourceCancellationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue<bool>("Workers:Enabled")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISourceCreationOperations>().ReconcileCancellationsAsync(ct);
                await scope.ServiceProvider.GetRequiredService<MedEquipSLA.Maintenance.Application.Features.Schedules.IScheduleTermination>()
                    .ReleaseBatchAsync(ct);
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            { logger.LogWarning(error, "Source cancellation reconciliation failed; pending is preserved."); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
