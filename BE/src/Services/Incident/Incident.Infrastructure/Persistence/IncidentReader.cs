using MedEquipSLA.Incident.Application.Features.Incidents;
using MedEquipSLA.Incident.Domain;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Incident.Infrastructure.Persistence;

public sealed class IncidentReader(IncidentDbContext db) : IIncidentReader
{
    private IQueryable<IncidentRecord> Scope(Guid? department) => department is { } id
        ? db.Incidents.AsNoTracking().Where(x => x.DepartmentIdSnapshot == id) : db.Incidents.AsNoTracking();
    public async Task<IncidentPage> SearchAsync(IncidentSearch input, CancellationToken ct)
    {
        var rows = Scope(input.DepartmentScope);
        if (!string.IsNullOrWhiteSpace(input.Search)) rows = rows.Where(x => x.Code.Contains(input.Search) || x.Title.Contains(input.Search) || x.EquipmentNameSnapshot.Contains(input.Search));
        if (input.Status is { } status) rows = rows.Where(x => x.Status == status);
        if (input.Priority is { } priority) rows = rows.Where(x => x.Priority == priority);
        if (input.EquipmentId is { } equipment) rows = rows.Where(x => x.EquipmentId == equipment);
        if (input.TechnicianId is { } technician) rows = rows.Where(x => x.AssignedTechnicianId == technician);
        var count = await rows.CountAsync(ct);
        var sorted = input.Sort switch
        {
            "oldest" => rows.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id),
            "deadline" => rows.OrderBy(x => x.ResolutionDeadlineAtUtc).ThenBy(x => x.Id),
            "code" => rows.OrderBy(x => x.Code).ThenBy(x => x.Id),
            _ => rows.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
        };
        var items = await sorted.Skip((input.Page - 1) * input.PageSize).Take(input.PageSize).Select(x => new IncidentRow(
            x.Id, x.Code, x.Title, x.Status, x.Priority, x.EquipmentId, x.EquipmentNameSnapshot, x.DepartmentIdSnapshot,
            x.AssignedTechnicianId, x.CreatedAtUtc, x.RowVersion)).ToArrayAsync(ct);
        return new(items, count, input.Page, input.PageSize);
    }
    public async Task<IncidentDetail?> FindAsync(Guid id, Guid? departmentScope, CancellationToken ct)
    {
        var row = await Scope(departmentScope).SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : new(new(row.Id, row.Code, row.Title, row.Status, row.Priority, row.EquipmentId,
            row.EquipmentNameSnapshot, row.DepartmentIdSnapshot, row.AssignedTechnicianId, row.CreatedAtUtc, row.RowVersion),
            row.Description, row.ReportedByUserId, row.ResponseDeadlineAtUtc, row.ResolutionDeadlineAtUtc,
            row.ClosedAtUtc, row.ResponseOutcome, row.ResolutionOutcome);
    }
}
