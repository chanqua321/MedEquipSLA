using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedEquipSLA.Maintenance.Infrastructure.Workers;

public sealed class RetirementSourceWorker(IServiceScopeFactory scopes, IConfiguration config,
    ILogger<RetirementSourceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue<bool>("Workers:Enabled") || !config.GetValue<bool>("Workers:Retirement:Enabled")) return;
        var page = 1;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var coordinator = scope.ServiceProvider.GetRequiredService<RetirementSourceCoordinator>();
                var intents = await coordinator.DiscoverAsync(page, ct);
                foreach (var intent in intents) await coordinator.CoordinateAsync(intent, ct);
                page = intents.Count >= 50 && page < 1_000_000 ? page + 1 : 1;
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            { logger.LogWarning(error, "Retirement source discovery failed; admission and registrations remain unchanged."); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
