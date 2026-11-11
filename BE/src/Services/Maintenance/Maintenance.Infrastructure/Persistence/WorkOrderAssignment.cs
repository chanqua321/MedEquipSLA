using MedEquipSLA.Maintenance.Application.Features.WorkOrders;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;

public sealed class WorkOrderAssignment(MaintenanceDbContext db) : IWorkOrderAssignment
{
    public async Task<AssignmentResult> AssignAsync(Guid id, Guid technician, byte[] rowVersion, Guid actor, CancellationToken ct, long authorizationVersion = 0)
    {
        var header = await db.WorkOrders.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.EquipmentId }).SingleOrDefaultAsync(ct);
        if (header is null) return AssignmentResult.NotFound;
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await EquipmentGateAccess.LockAsync(db, header.EquipmentId, ct);
        var order = await db.WorkOrders.FromSqlInterpolated(
            $"SELECT * FROM dbo.WorkOrders WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleAsync(ct);
        if (!rowVersion.SequenceEqual(order.RowVersion) || order.Status is not ("Scheduled" or "InProgress"))
            return AssignmentResult.Conflict;
        if (order.AssignedTechnicianId == technician) return AssignmentResult.Saved;
        var operation = await db.IdempotencyRequests.AsNoTracking().SingleOrDefaultAsync(x => x.ResourceId == id &&
            x.OperationName == "CreateWorkOrder" && x.State == "Committed", ct);
        if (operation?.RequestPayloadJson is not { } json) return AssignmentResult.Conflict;

        var now = DateTimeOffset.UtcNow;
        var previous = order.AssignedTechnicianId;
        order.Assign(technician, actor, now, authorizationVersion);
        db.WorkOrderActivities.Add(WorkOrderActivity.Assignment(id, order.Status, previous, technician, actor, now));
        db.Add(AuditEntry.ForOperation(operation.OperationId, "WorkOrderAssigned", now, operation.OperationId.ToString(),
            JsonSerializer.Serialize(new { id, previous, technician }), actor));
        db.Add(new OutboxMessage(new UserNotificationEvent("WorkOrderAssigned", "Recipient", technician, "WorkOrder", order.Id,
            order.DepartmentIdSnapshot, "Bạn được phân công công việc", $"Công việc {order.Code} cho thiết bị {order.EquipmentCodeSnapshot}.")
            .Envelope("Maintenance", order.AggregateVersion, now, operation.OperationId.ToString()), "WorkOrder"));
        var payload = new WorkOrderLifecycleEvent(order.EquipmentId, order.EquipmentUseRegistrationId, operation.OperationId,
            operation.FencingToken, json, order.Status, order.IsBlocking);
        db.Add(new OutboxMessage(new IntegrationEvent(Guid.NewGuid(), WorkOrderLifecycleEvent.Updated, 1, "Maintenance",
            order.Id, order.AggregateVersion, now, operation.OperationId.ToString(), JsonSerializer.Serialize(payload)), "WorkOrder"));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return AssignmentResult.Conflict; }
        await tx.CommitAsync(ct);
        return AssignmentResult.Saved;
    }
}
