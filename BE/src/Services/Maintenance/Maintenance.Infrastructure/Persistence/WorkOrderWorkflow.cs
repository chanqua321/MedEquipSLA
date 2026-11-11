using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
using MedEquipSLA.Maintenance.Application.Features.WorkOrders;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;

public sealed class WorkOrderWorkflow(MaintenanceDbContext db, EquipmentLookup.EquipmentLookupClient equipment,
    IConfiguration config, TimeProvider time) : IWorkOrderWorkflow
{
    public Task<WorkflowStatus> ProgressAsync(Guid id, Guid actor, byte[] version, string notes, CancellationToken ct) =>
        ChangeAsync(id, actor, version, "ProgressRecorded", notes.Trim(), (order, _) =>
        {
            if (notes.Trim().Length is < 1 or > 2000) throw new ArgumentException("Progress notes required.");
            if (order.AssignedTechnicianId != actor) throw new UnauthorizedAccessException();
            order.RecordProgress(actor, time.GetUtcNow());
            return Task.CompletedTask;
        }, ct);
    public async Task<WorkflowStatus> StartAsync(Guid id, Guid actor, byte[] version, CancellationToken ct)
    {
        var row = await db.WorkOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return WorkflowStatus.NotFound;
        if (row.AssignedTechnicianId != actor) return WorkflowStatus.Forbidden;
        if (row.Status != "Scheduled" || !row.RowVersion.SequenceEqual(version)) return WorkflowStatus.Conflict;
        var operation = await db.IdempotencyRequests.AsNoTracking().SingleOrDefaultAsync(x => x.ResourceId == id &&
            x.OperationName == "CreateWorkOrder" && x.State == "Committed", ct);
        if (operation?.RequestPayloadJson is not { } json) return WorkflowStatus.Conflict;
        var payload = WorkOrderRegistrationPayload.Parse(json);
        var key = config["Grpc:MaintenanceWorkOrderKey"];
        if (string.IsNullOrWhiteSpace(key)) return WorkflowStatus.Unavailable;
        try
        {
            var confirmation = await equipment.ConfirmWorkOrderCommittedAsync(new()
            {
                Registration = new() { EquipmentId = payload.EquipmentId.ToString(), ScheduleId = payload.ScheduleId.ToString(),
                    OccurrenceId = payload.OccurrenceId.ToString(), WorkOrderId = payload.WorkOrderId.ToString(),
                    OperationId = operation.OperationId.ToString(), WorkType = payload.WorkType, FencingToken = operation.FencingToken,
                    ScheduledForUtc = Timestamp.FromDateTimeOffset(payload.ScheduledForUtc), DueAtUtc = Timestamp.FromDateTimeOffset(payload.DueAtUtc),
                    RequestHash = ByteString.CopyFrom(payload.Hash()), CorrelationId = Guid.NewGuid().ToString() },
                RegistrationId = row.EquipmentUseRegistrationId.ToString(), SourceVersion = 1
            }, new Metadata { { "x-maintenance-workorder-key", key } }, DateTime.UtcNow.AddSeconds(3), ct);
            if (!confirmation.Found || confirmation.State != "Active" || confirmation.RegistrationId != row.EquipmentUseRegistrationId.ToString())
                return WorkflowStatus.Conflict;
        }
        catch (RpcException) when (!ct.IsCancellationRequested) { return WorkflowStatus.Unavailable; }
        return await ChangeAsync(id, actor, version, "Started", null, (order, _) =>
        { order.Start(actor, time.GetUtcNow()); return Task.CompletedTask; }, ct);
    }

    public Task<WorkflowStatus> SubmitAsync(Guid id, Guid actor, byte[] version, WorkOrderReport report, CancellationToken ct) =>
        ChangeAsync(id, actor, version, "Completed", null, async (order, token) =>
        {
            if (order.AssignedTechnicianId != actor) throw new UnauthorizedAccessException();
            var now = time.GetUtcNow();
            var next = (await db.WorkOrderAttempts.Where(x => x.WorkOrderId == id).Select(x => (int?)x.AttemptNumber).MaxAsync(token) ?? 0) + 1;
            var attempt = WorkOrderAttempt.Submit(id, next, actor, report.Result, report.Notes, report.Cost, report.Currency,
                report.Photo, report.ContentType, now);
            if (order.WorkType == "Calibration")
            {
                if (report.Measurements.Count is < 1 or > 100 ||
                    report.Measurements.Any(x => x is null || string.IsNullOrWhiteSpace(x.Code) ||
                        string.IsNullOrWhiteSpace(x.Name) || string.IsNullOrWhiteSpace(x.Unit)) ||
                    report.Measurements.Select(x => x.Code.Trim().ToUpperInvariant()).Distinct().Count() != report.Measurements.Count)
                    throw new ArgumentException("Calibration requires distinct measurements.");
                var measurements = report.Measurements.Select(x => CalibrationMeasurement.Record(attempt.Id, x.Code,
                    x.Name, x.Unit, x.Value, x.Minimum, x.Maximum)).ToArray();
                if ((measurements.All(x => x.Passed) ? "Passed" : "Failed") != report.Result)
                    throw new ArgumentException("Conclusion must match measured limits.");
                db.AddRange(measurements);
            }
            else if (report.Measurements.Count != 0) throw new ArgumentException("Measurements require Calibration work.");
            order.Complete(actor, now);
            db.Add(attempt);
        }, ct);

    public Task<WorkflowStatus> ReviewAsync(Guid id, Guid actor, byte[] version, bool accept, string? reason, CancellationToken ct) =>
        ChangeAsync(id, actor, version, accept ? "Accepted" : "Rejected", reason, async (order, token) =>
        {
            var attempt = await db.WorkOrderAttempts.Where(x => x.WorkOrderId == id && x.ReviewDecision == "Pending")
                .OrderByDescending(x => x.AttemptNumber).FirstOrDefaultAsync(token);
            if (attempt is null) throw new InvalidOperationException("No pending completion attempt.");
            order.Review(attempt, actor, accept, reason, time.GetUtcNow());
        }, ct);
    public Task<WorkflowStatus> CancelAsync(Guid id, Guid actor, byte[] version, string reason, CancellationToken ct) =>
        ChangeAsync(id, actor, version, "Cancelled", reason, (order, _) =>
        { order.Cancel(actor, reason, time.GetUtcNow()); return Task.CompletedTask; }, ct);

    private async Task<WorkflowStatus> ChangeAsync(Guid id, Guid actor, byte[] version, string action, string? reason,
        Func<WorkOrder,CancellationToken,Task> change, CancellationToken ct)
    {
        var header = await db.WorkOrders.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.EquipmentId }).SingleOrDefaultAsync(ct);
        if (header is null) return WorkflowStatus.NotFound;
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var gate = await EquipmentGateAccess.LockAsync(db, header.EquipmentId, ct, time.GetUtcNow());
        var order = await db.WorkOrders.FromSqlInterpolated(
            $"SELECT * FROM dbo.WorkOrders WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleAsync(ct);
        if (gate.Phase == "Retired" || !version.SequenceEqual(order.RowVersion)) return WorkflowStatus.Conflict;
        var operation = await db.IdempotencyRequests.AsNoTracking().SingleOrDefaultAsync(x => x.ResourceId == id &&
            x.OperationName == "CreateWorkOrder" && x.State == "Committed", ct);
        if (operation?.RequestPayloadJson is not { } json) return WorkflowStatus.Conflict;
        var before = order.Status;
        try { await change(order, ct); }
        catch (UnauthorizedAccessException) { return WorkflowStatus.Forbidden; }
        catch (ArgumentException) { return WorkflowStatus.Invalid; }
        catch (InvalidOperationException) { return WorkflowStatus.Conflict; }
        var now = time.GetUtcNow();
        db.Add(WorkOrderActivity.Transition(order.Id, action, before, order.Status, actor, reason, now));
        if (action == "Completed" || action == "Accepted" && order.AssignedTechnicianId is not null)
            db.Add(new OutboxMessage(new UserNotificationEvent("WorkOrderCompleted", action == "Completed" ? "Admins" : "Recipient",
                action == "Completed" ? null : order.AssignedTechnicianId, "WorkOrder", order.Id, order.DepartmentIdSnapshot,
                action == "Completed" ? "Công việc chờ nghiệm thu" : "Công việc đã được nghiệm thu", $"Kiểm tra hồ sơ {order.Code}.")
                .Envelope("Maintenance", order.AggregateVersion, now, operation.OperationId.ToString()), "WorkOrder"));
        db.Add(AuditEntry.ForOperation(operation.OperationId, "WorkOrder" + action, now, operation.OperationId.ToString(),
            JsonSerializer.Serialize(new { order.Id, before, after = order.Status, reason }), actor));
        var payload = new WorkOrderLifecycleEvent(order.EquipmentId, order.EquipmentUseRegistrationId, operation.OperationId,
            operation.FencingToken, json, order.Status, order.IsBlocking);
        db.Add(new OutboxMessage(new IntegrationEvent(Guid.NewGuid(), WorkOrderLifecycleEvent.Updated, 1, "Maintenance",
            order.Id, order.AggregateVersion, now, operation.OperationId.ToString(), JsonSerializer.Serialize(payload)), "WorkOrder"));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return WorkflowStatus.Conflict; }
        await tx.CommitAsync(ct);
        return WorkflowStatus.Saved;
    }
}
