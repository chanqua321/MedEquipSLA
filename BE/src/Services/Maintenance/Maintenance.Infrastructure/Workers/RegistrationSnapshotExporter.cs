using System.Data;
using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Maintenance.Infrastructure.Persistence;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedEquipSLA.Maintenance.Infrastructure.Workers;

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
                await CaptureAsync(scope.ServiceProvider.GetRequiredService<MaintenanceDbContext>(),DateTimeOffset.UtcNow,ct);
            }
            catch (Exception e) when (!ct.IsCancellationRequested) { logger.LogWarning(e,"Maintenance registration snapshot capture failed; local transaction rolled back."); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
    public static async Task<RegistrationSnapshotPage[]> CaptureAsync(MaintenanceDbContext db, DateTimeOffset now, CancellationToken ct, int pageSize = 100)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result=sp_getapplock @Resource='Maintenance.RegistrationSnapshot',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
            IF @result<0 THROW 51076,'Snapshot capture lock unavailable.',1;
            """,ct);
        if (await db.OutboxMessages.AnyAsync(x => x.EventType == RegistrationSnapshotPage.EventType &&
            (x.OccurredAtUtc >= now.AddMinutes(-5) || x.Status == "Pending" || x.Status == "Processing"),ct)) return [];
        var schedules = await db.MaintenanceSchedules.AsNoTracking().ToArrayAsync(ct);
        var orders = await db.WorkOrders.AsNoTracking().ToArrayAsync(ct);
        var operations = await db.IdempotencyRequests.AsNoTracking().Where(x => x.OperationName == "CreateWorkOrder" || x.OperationName == "CreateMaintenanceSchedule").ToArrayAsync(ct);
        var linked = new HashSet<Guid>(); var items = new List<RegistrationSnapshotItem>();
        foreach (var schedule in schedules)
        {
            var op = operations.SingleOrDefault(x => x.OperationName == "CreateMaintenanceSchedule" && x.ResourceId == schedule.Id);
            if (op is not null) linked.Add(op.OperationId);
            items.Add(new("MaintenanceSchedule",schedule.Id,op?.OperationId ?? Guid.Empty,schedule.EquipmentId,
                schedule.EquipmentUseRegistrationId,op?.State == "Committed" && op.EquipmentId == schedule.EquipmentId ? "Committed" : "Unknown",
                schedule.AggregateVersion,schedule.EndedAtUtc is not null ? "Ended" : schedule.IsActive ? "Active" : "Inactive",
                ScheduleReleaseConfirmed:schedule.RegistrationReleasedAtUtc is not null));
        }
        foreach (var order in orders)
        {
            var op = operations.SingleOrDefault(x => x.OperationName == "CreateWorkOrder" && x.ResourceId == order.Id);
            if (op is not null) linked.Add(op.OperationId);
            var proven = false;
            if(op?.State == "Committed" && op.EquipmentId == order.EquipmentId && op.FencingToken > 0 && op.RequestPayloadJson is { } json)
            {
                try
                {
                    var payload=MedEquipSLA.Maintenance.Domain.WorkOrderRegistrationPayload.Parse(json);
                    proven=payload.WorkOrderId==order.Id && payload.EquipmentId==order.EquipmentId && payload.OccurrenceId==order.OccurrenceId &&
                        payload.Hash().SequenceEqual(op.RequestHash);
                }
                catch(Exception error) when(error is JsonException or InvalidOperationException or FormatException or KeyNotFoundException) { }
            }
            items.Add(new("WorkOrder",order.Id,op?.OperationId ?? Guid.Empty,order.EquipmentId,order.EquipmentUseRegistrationId,
                proven ? "Committed" : "Unknown",
                order.AggregateVersion,order.Status,op?.FencingToken ?? 0,op?.RequestPayloadJson));
        }
        foreach (var op in operations.Where(x => !linked.Contains(x.OperationId)))
            items.Add(new(op.OperationName == "CreateWorkOrder" ? "WorkOrder" : "MaintenanceSchedule",op.ResourceId ?? op.OperationId,
                op.OperationId,op.EquipmentId,null,op.State == "Committed" ? "Unknown" : op.State,0,op.CancelIntentId is not null ? "CancelRequested" : "Unresolved"));
        var pages = RegistrationSnapshotPage.Capture(items,now,pageSize);
        db.AddRange(pages.Select(x => new OutboxMessage(x.Envelope("Maintenance"),"RegistrationSnapshot")));
        db.Add(AuditEntry.ForOperation(pages[0].SnapshotId,"RegistrationSnapshotCaptured",now,pages[0].SnapshotId.ToString(),
            JsonSerializer.Serialize(new { total = items.Count,pages = pages.Length,watermark = pages[0].Watermark })));
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return pages;
    }
}
