using System.Data;
using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Incident.Infrastructure.Persistence;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedEquipSLA.Incident.Infrastructure.Workers;

public sealed class RegistrationSnapshotExporter(IConfiguration config, IServiceScopeFactory scopes,
    ILogger<RegistrationSnapshotExporter> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue<bool>("Workers:Enabled")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await CaptureAsync(scope.ServiceProvider.GetRequiredService<IncidentDbContext>(),DateTimeOffset.UtcNow,ct);
            }
            catch (Exception e) when (!ct.IsCancellationRequested) { logger.LogWarning(e,"Incident registration snapshot capture failed; local transaction rolled back."); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
    public static async Task<RegistrationSnapshotPage[]> CaptureAsync(IncidentDbContext db, DateTimeOffset now, CancellationToken ct, int pageSize = 100)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result=sp_getapplock @Resource='Incident.RegistrationSnapshot',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
            IF @result<0 THROW 51076,'Snapshot capture lock unavailable.',1;
            """,ct);
        if (await db.OutboxMessages.AnyAsync(x => x.EventType == RegistrationSnapshotPage.EventType &&
            (x.OccurredAtUtc >= now.AddMinutes(-5) || x.Status == "Pending" || x.Status == "Processing"),ct)) return [];
        var incidents = await db.Incidents.AsNoTracking().ToArrayAsync(ct);
        var operations = await db.IdempotencyRequests.AsNoTracking().Where(x => x.OperationName == "CreateIncident").ToArrayAsync(ct);
        var linked = new HashSet<Guid>();var items = new List<RegistrationSnapshotItem>();
        foreach (var incident in incidents)
        {
            var op = operations.SingleOrDefault(x => x.ResourceId == incident.Id);
            if (op is not null) linked.Add(op.OperationId);
            items.Add(new("Incident",incident.Id,op?.OperationId ?? Guid.Empty,incident.EquipmentId,incident.EquipmentUseRegistrationId,
                op?.State == "Committed" && op.EquipmentId == incident.EquipmentId ? "Committed" : "Unknown",incident.AggregateVersion,incident.Status));
        }
        foreach (var op in operations.Where(x => !linked.Contains(x.OperationId)))
            items.Add(new("Incident",op.ResourceId ?? op.OperationId,op.OperationId,op.EquipmentId,null,
                op.State == "Committed" ? "Unknown" : op.State,0,op.CancelIntentId is not null ? "CancelRequested" : "Unresolved"));
        var pages = RegistrationSnapshotPage.Capture(items,now,pageSize);
        db.AddRange(pages.Select(x => new OutboxMessage(x.Envelope("Incident"))));
        db.Add(AuditEntry.ForCreationOperation(pages[0].SnapshotId,"RegistrationSnapshotCaptured",now,pages[0].SnapshotId.ToString(),
            JsonSerializer.Serialize(new { total = items.Count,pages = pages.Length,watermark = pages[0].Watermark })));
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return pages;
    }
}
