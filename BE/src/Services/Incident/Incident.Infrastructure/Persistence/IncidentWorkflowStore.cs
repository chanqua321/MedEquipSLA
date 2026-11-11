using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Incident.Application.Features.Incidents;
using MedEquipSLA.Incident.Domain;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Incident.Infrastructure.Persistence;

public sealed class IncidentWorkflowStore(IncidentDbContext db) : IIncidentWorkflowStore
{
    public async Task<IncidentWorkflowStatus> AssignAsync(Guid id, IncidentAssignmentInput input, Guid actor, long authorizationVersion, string correlation, CancellationToken ct)
    {
        if (input.TechnicianId == Guid.Empty || input.RowVersion is not { Length: 8 }) return IncidentWorkflowStatus.Invalid;
        var incident = await db.Incidents.FindAsync([id], ct);
        if (incident is null) return IncidentWorkflowStatus.NotFound;
        if (!input.RowVersion.SequenceEqual(incident.RowVersion) || incident.Status is not ("Open" or "InProgress"))
            return IncidentWorkflowStatus.Conflict;

        if (incident.AssignedTechnicianId == input.TechnicianId) return IncidentWorkflowStatus.Saved;
        var now = DateTimeOffset.UtcNow;
        var previous = incident.AssignedTechnicianId;
        incident.Assign(input.TechnicianId, actor, now, authorizationVersion);
        db.IncidentActivities.Add(IncidentActivity.Record(id, "Assigned", actor, now,
            incident.Status, incident.Status, previous, input.TechnicianId));
        db.Add(AuditEntry.ForIncident(actor, id, "IncidentAssigned", now, correlation,
            JsonSerializer.Serialize(new { previous, input.TechnicianId })));
        db.Add(new OutboxMessage(new UserNotificationEvent("IncidentAssigned", "Recipient", input.TechnicianId, "Incident",
            id, incident.DepartmentIdSnapshot, "Bạn được phân công sửa thiết bị",
            $"Kiểm tra sự cố {incident.Code} của thiết bị {incident.EquipmentCodeSnapshot}.")
            .Envelope("Incident", incident.AggregateVersion, now, correlation)));
        return await SaveAsync(db, ct);
    }

    public async Task<IncidentWorkflowStatus> StartAsync(Guid id, IncidentVersionInput input, Guid actor, string correlation, CancellationToken ct)
    {
        if (input.RowVersion is not { Length: 8 }) return IncidentWorkflowStatus.Invalid;
        var incident = await db.Incidents.FindAsync([id], ct);
        if (incident is null) return IncidentWorkflowStatus.NotFound;
        if (incident.AssignedTechnicianId != actor) return IncidentWorkflowStatus.Forbidden;
        if (!input.RowVersion.SequenceEqual(incident.RowVersion) || incident.Status != "Open") return IncidentWorkflowStatus.Conflict;
        var now = DateTimeOffset.UtcNow;
        incident.StartRepair(actor, now);
        db.IncidentActivities.Add(IncidentActivity.Record(id, "Started", actor, now, "Open", "InProgress", actor, actor));
        db.Add(AuditEntry.ForIncident(actor, id, "IncidentStarted", now, correlation, "{}"));
        return await SaveAsync(db, ct);
    }

    public async Task<IncidentWorkflowStatus> RejectAsync(Guid id, Guid attemptId, IncidentRejectionInput input, Guid actor, string correlation, CancellationToken ct)
    {
        var reason = input.Reason?.Trim();
        if (reason is not { Length: > 0 and <= 2000 } || input.RowVersion is not { Length: 8 })
            return IncidentWorkflowStatus.Invalid;
        var incident = await db.Incidents.FindAsync([id], ct);
        if (incident is null) return IncidentWorkflowStatus.NotFound;
        if (!input.RowVersion.SequenceEqual(incident.RowVersion) || incident.Status != "Resolved") return IncidentWorkflowStatus.Conflict;
        var latest = await db.ResolutionAttempts.Where(x => x.IncidentId == id)
            .OrderByDescending(x => x.AttemptNumber).FirstOrDefaultAsync(ct);
        if (latest is null || latest.Id != attemptId || latest.ReviewDecision != "Pending") return IncidentWorkflowStatus.Conflict;

        var now = DateTimeOffset.UtcNow;
        latest.Reject(actor, reason, now);
        incident.ReturnForRepair(actor, now);
        db.IncidentActivities.Add(IncidentActivity.Record(id, "RepairRejected", actor, now, "Resolved", "InProgress",
            incident.AssignedTechnicianId, incident.AssignedTechnicianId, reason));
        db.Add(AuditEntry.ForIncident(actor, id, "IncidentRepairRejected", now, correlation, JsonSerializer.Serialize(new { reason, attemptId })));
        return await SaveAsync(db, ct);
    }

    public async Task<IncidentWorkflowStatus> AcceptAsync(Guid id, Guid attemptId, IncidentVersionInput input, Guid actor, string correlation, CancellationToken ct)
    {
        if (input.RowVersion is not { Length: 8 }) return IncidentWorkflowStatus.Invalid;
        var incident = await db.Incidents.FindAsync([id], ct);
        if (incident is null) return IncidentWorkflowStatus.NotFound;
        if (!input.RowVersion.SequenceEqual(incident.RowVersion) || incident.Status != "Resolved") return IncidentWorkflowStatus.Conflict;
        var latest = await db.ResolutionAttempts.Where(x => x.IncidentId == id)
            .OrderByDescending(x => x.AttemptNumber).FirstOrDefaultAsync(ct);
        var now = DateTimeOffset.UtcNow;
        if (latest is null || latest.Id != attemptId || latest.ReviewDecision != "Pending" ||
            latest.OperationPhoto is not { Length: > 0 } || latest.CompletedAtUtc > now)
            return IncidentWorkflowStatus.Conflict;

        latest.Accept(actor, now);
        incident.Close(latest, actor, now);
        db.IncidentActivities.Add(IncidentActivity.Record(id, "RepairAccepted", actor, now, "Resolved", "Closed",
            incident.AssignedTechnicianId, incident.AssignedTechnicianId));
        db.Add(AuditEntry.ForIncident(actor, id, "IncidentClosed", now, correlation, JsonSerializer.Serialize(new { attemptId })));
        db.Add(new OutboxMessage(new UserNotificationEvent("IncidentClosed", "Recipient", incident.ReportedByUserId, "Incident", id,
            incident.DepartmentIdSnapshot, "Sự cố đã được nghiệm thu", $"Sự cố {incident.Code} đã hoàn tất và được đóng.")
            .Envelope("Incident", incident.AggregateVersion, now, correlation)));
        var payload = new IncidentLifecycleEvent(incident.EquipmentId, incident.EquipmentUseRegistrationId, incident.Status);
        db.OutboxMessages.Add(new OutboxMessage(new IntegrationEvent(Guid.NewGuid(), IncidentLifecycleEvent.Closed,
            1, "Incident", id, incident.AggregateVersion, now, correlation, JsonSerializer.Serialize(payload))));
        return await SaveAsync(db, ct);
    }

    private static async Task<IncidentWorkflowStatus> SaveAsync(IncidentDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return IncidentWorkflowStatus.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            return IncidentWorkflowStatus.Conflict;
        }
    }

}
