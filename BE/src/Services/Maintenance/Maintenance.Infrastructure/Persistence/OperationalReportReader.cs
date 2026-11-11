using MedEquipSLA.Maintenance.Application.Features.Reports;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;

public sealed class OperationalReportReader(MaintenanceDbContext db) : IOperationalReportReader
{
    public async Task<ReportPage> ReadAsync(ReportFilter filter, CancellationToken ct)
    {
        var rows = db.WorkOrders.AsNoTracking();
        if (filter.DepartmentId is { } department) rows = rows.Where(x => x.DepartmentIdSnapshot == department);
        if (filter.Status is { } status) rows = rows.Where(x => x.Status == status);
        if (filter.FromUtc is { } from) rows = rows.Where(x => x.ScheduledForUtc >= from);
        if (filter.UntilUtc is { } until) rows = rows.Where(x => x.ScheduledForUtc < until);
        var page = await rows.OrderBy(x => x.ScheduledForUtc).ThenBy(x => x.Id).Skip((filter.Page - 1) * 1000).Take(1001)
            .Select(x => new ReportRow(x.Id, x.Code, x.EquipmentNameSnapshot, x.DepartmentIdSnapshot, x.Status, x.ScheduledForUtc, null, null)).ToArrayAsync(ct);
        return new(page.Take(1000).ToArray(), page.Length > 1000, DateTimeOffset.UtcNow, "WorkOrder history by department snapshot; timestamp is scheduled time, not completion time.");
    }
}
