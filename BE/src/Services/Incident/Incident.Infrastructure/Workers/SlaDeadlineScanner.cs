using System.Data;
using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Incident.Domain;
using MedEquipSLA.Incident.Infrastructure.Persistence;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedEquipSLA.Incident.Infrastructure.Workers;

public sealed class SlaDeadlineScanner(IConfiguration config, IServiceScopeFactory scopes, ILogger<SlaDeadlineScanner> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue<bool>("Workers:Enabled"))
            return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(config.GetValue("Workers:SlaIntervalSeconds", 60), 1, 3600)));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IncidentDbContext>();
                var count = await ScanAsync(db, DateTimeOffset.UtcNow, stoppingToken);
                if (count != 0)
                    logger.LogInformation("Recorded {Count} provisional SLA alerts", count);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "SLA scan failed. The next tick will retry.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public static async Task<int> ScanAsync(IncidentDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var open = db.Incidents.AsNoTracking().Where(x => x.Status != "Closed");
        var response = await open.Where(x => x.ResponseDeadlineAtUtc < now &&
                (x.FirstRespondedAtUtc == null || x.FirstRespondedAtUtc > x.ResponseDeadlineAtUtc) &&
                !db.SlaDeadlineAlerts.Any(a => a.IncidentId == x.Id && a.AlertKind == "Response"))
            .OrderBy(x => x.ResponseDeadlineAtUtc).ThenBy(x => x.Id).Take(50).ToListAsync(ct);
        var resolution = await open.Where(x => x.ResolutionDeadlineAtUtc < now &&
                !db.ResolutionAttempts.Any(a => a.IncidentId == x.Id && a.ReviewDecision == "Accepted" &&
                    a.CompletedAtUtc <= x.ResolutionDeadlineAtUtc) &&
                !db.SlaDeadlineAlerts.Any(a => a.IncidentId == x.Id && a.AlertKind == "Resolution"))
            .OrderBy(x => x.ResolutionDeadlineAtUtc).ThenBy(x => x.Id).Take(50).ToListAsync(ct);

        foreach (var incident in response)
            AddAlert(db, incident, "Response", incident.ResponseDeadlineAtUtc, now);
        foreach (var incident in resolution)
            AddAlert(db, incident, "Resolution", incident.ResolutionDeadlineAtUtc, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return response.Count + resolution.Count;
    }

    private static void AddAlert(IncidentDbContext db, IncidentRecord incident, string kind,
        DateTimeOffset deadline, DateTimeOffset now)
    {
        var eventId = Guid.NewGuid();
        var type = kind == "Response" ? SlaAlertEvent.ResponseBreached : SlaAlertEvent.ResolutionBreached;
        var recipient = incident.AssignedTechnicianId ?? incident.ReportedByUserId;
        var payload = new SlaAlertEvent(recipient, incident.DepartmentIdSnapshot, deadline);
        db.SlaDeadlineAlerts.Add(new SlaDeadlineAlert(incident.Id, kind, deadline, now, eventId));
        db.OutboxMessages.Add(new OutboxMessage(new IntegrationEvent(eventId, type, 1, "Incident",
            incident.Id, incident.AggregateVersion, now, eventId.ToString(), JsonSerializer.Serialize(payload))));
    }
}
