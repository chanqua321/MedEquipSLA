using MedEquipSLA.Maintenance.Application.Features.Schedules;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;

public sealed class ScheduleReader(MaintenanceDbContext db) : IScheduleReader
{
    private const int PageSize = 20;

    public async Task<ScheduleDetail?> FindAsync(Guid id, CancellationToken ct)
    {
        var row = await db.MaintenanceSchedules.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : Detail(row);
    }

    internal static ScheduleDetail Detail(MedEquipSLA.Maintenance.Domain.MaintenanceSchedule row) =>
        new(row.Id, row.Code, row.EquipmentId, row.EquipmentCodeSnapshot, row.EquipmentNameSnapshot,
            row.DepartmentIdSnapshot, row.DepartmentNameSnapshot, row.WorkType,
            new(row.AnchorAtUtc, row.IntervalDays, row.ReminderLeadMinutes, row.CompletionWindowMinutes, row.IsActive),
            row.NextOccurrenceAtUtc, row.RowVersion, row.EndedAtUtc, row.EndedByUserId, row.EndReason, row.RegistrationReleasedAtUtc);

    public async Task<SchedulePage<ScheduleRow>> ListAsync(ScheduleSearch search, CancellationToken ct)
    {
        var query = db.MaintenanceSchedules.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search.Search))
            query = query.Where(x => x.Code.Contains(search.Search) || x.EquipmentNameSnapshot.Contains(search.Search));
        if (search.Active is { } active)
            query = query.Where(x => x.IsActive == active);

        var rows = await query.OrderBy(x => x.NextOccurrenceAtUtc).ThenBy(x => x.Id)
            .Skip((search.Page - 1) * PageSize).Take(PageSize + 1)
            .Select(x => new ScheduleRow(x.Id, x.Code, x.EquipmentNameSnapshot, x.DepartmentNameSnapshot,
                x.WorkType, x.IsActive, x.IntervalDays, x.ReminderLeadMinutes, x.CompletionWindowMinutes,
                x.NextOccurrenceAtUtc)).ToListAsync(ct);
        return new(rows.Take(PageSize).ToArray(), rows.Count > PageSize);
    }

    public async Task<SchedulePage<OccurrenceRow>?> OccurrencesAsync(Guid scheduleId, int page, CancellationToken ct)
    {
        if (!await db.MaintenanceSchedules.AnyAsync(x => x.Id == scheduleId, ct)) return null;

        var rows = await db.ScheduleOccurrences.AsNoTracking().Where(x => x.ScheduleId == scheduleId)
            .OrderByDescending(x => x.ScheduledOccurrenceAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * PageSize).Take(PageSize + 1)
            .Select(x => new OccurrenceRow(x.Id, x.ScheduledOccurrenceAtUtc, x.DueAtUtc, x.State,
                db.WorkOrders.Where(order => order.OccurrenceId == x.Id).Select(order => (Guid?)order.Id).FirstOrDefault()))
            .ToListAsync(ct);
        return new(rows.Take(PageSize).ToArray(), rows.Count > PageSize);
    }
}
