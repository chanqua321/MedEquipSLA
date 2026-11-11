using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Incident.Application.Features.Incidents;
using MedEquipSLA.Incident.Domain;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Incident.Infrastructure.Persistence;

public sealed class IncidentRepairStore(IncidentDbContext db) : IIncidentRepairStore
{
    private IQueryable<IncidentRecord> Scope(Guid? department) => db.Incidents.AsNoTracking().Where(x => department == null || x.DepartmentIdSnapshot == department);
    public async Task<IReadOnlyList<IncidentRepairRow>> ListAsync(Guid? id, Guid? department, CancellationToken ct) =>
        await Scope(department).Where(x => id == null || x.Id == id).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Take(50)
            .Select(x => new IncidentRepairRow(x.Id, x.Code, x.Title, x.Status, x.AssignedTechnicianId, x.RowVersion,
                x.EquipmentId, x.EquipmentNameSnapshot, x.ResponseOutcome, x.ResolutionOutcome)).ToArrayAsync(ct);
    public async Task<IReadOnlyList<IncidentAttemptRow>?> AttemptsAsync(Guid id, Guid? department, CancellationToken ct)
    {
        if (!await Scope(department).AnyAsync(x => x.Id == id, ct)) return null;
        return await db.ResolutionAttempts.AsNoTracking().Where(x => x.IncidentId == id).OrderByDescending(x => x.AttemptNumber)
            .Select(x => new IncidentAttemptRow(x.Id, x.AttemptNumber, x.ResolutionNotes, x.ResultSummary, x.CompletedAtUtc,
                x.ReviewDecision, x.ReviewReason, x.OperationPhotoContentType != null)).ToArrayAsync(ct);
    }
    public async Task<IncidentPhoto?> PhotoAsync(Guid id, Guid attempt, Guid? department, CancellationToken ct)
    {
        if (!await Scope(department).AnyAsync(x => x.Id == id, ct)) return null;
        var row = await db.ResolutionAttempts.AsNoTracking().Where(x => x.IncidentId == id && x.Id == attempt)
            .Select(x => new { x.OperationPhoto, x.OperationPhotoContentType }).SingleOrDefaultAsync(ct);
        return row?.OperationPhoto is { } bytes && row.OperationPhotoContentType is { } type ? new(bytes, type) : null;
    }
    public async Task<IncidentRepairResult> SubmitAsync(Guid id, Guid actor, bool admin, IncidentRepairReport report, string correlation, CancellationToken ct)
    {
        var incident = await db.Incidents.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (incident is null) return new(IncidentWorkflowStatus.NotFound);
        if (!admin && incident.AssignedTechnicianId != actor) return new(IncidentWorkflowStatus.Forbidden);
        if (incident.Status != "InProgress" || !report.RowVersion.SequenceEqual(incident.RowVersion))
            return new(IncidentWorkflowStatus.Conflict);

        var previous = await db.ResolutionAttempts.Where(x => x.IncidentId == id)
            .MaxAsync(x => (int?)x.AttemptNumber, ct) ?? 0;
        var now = DateTimeOffset.UtcNow;
        var attempt = ResolutionAttempt.Submit(id, previous + 1, actor, report.Notes.Trim(), report.Result.Trim(), now, report.Photo, report.ContentType);
        incident.SubmitRepair(actor, now);
        db.ResolutionAttempts.Add(attempt);
        db.IncidentActivities.Add(IncidentActivity.Record(id, "RepairSubmitted", actor, now, "InProgress", "Resolved",
            incident.AssignedTechnicianId, incident.AssignedTechnicianId));
        db.Add(AuditEntry.ForIncident(actor, id, "IncidentResolved", now, correlation,
            JsonSerializer.Serialize(new { attempt.Id, attempt.AttemptNumber })));
        db.Add(new OutboxMessage(new UserNotificationEvent("IncidentResolved", "Admins", null, "Incident", id,
            incident.DepartmentIdSnapshot, "Sự cố chờ nghiệm thu", $"Kiểm tra biên bản và ảnh vận hành của sự cố {incident.Code}.")
            .Envelope("Incident", incident.AggregateVersion, now, correlation)));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(IncidentWorkflowStatus.Conflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            return new(IncidentWorkflowStatus.Conflict);
        }
        return new(IncidentWorkflowStatus.Saved, attempt.Id, attempt.AttemptNumber);
    }
}
