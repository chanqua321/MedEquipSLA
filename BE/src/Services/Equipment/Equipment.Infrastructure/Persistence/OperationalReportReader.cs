using MedEquipSLA.Equipment.Application.Features.Reports;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence;

public sealed class OperationalReportReader(EquipmentDbContext db) : IOperationalReportReader
{
    public async Task<ReportPage> ReadAsync(ReportFilter filter, CancellationToken ct)
    {
        var rows = db.EquipmentAssets.AsNoTracking();
        if (filter.DepartmentId is { } department) rows = rows.Where(x => x.DepartmentId == department);
        if (filter.Status is { } status) rows = rows.Where(x => x.OperationalStatus == status);
        if (filter.FromUtc is { } from) rows = rows.Where(x => x.UpdatedAtUtc >= from);
        if (filter.UntilUtc is { } until) rows = rows.Where(x => x.UpdatedAtUtc < until);
        var page = await rows.OrderBy(x => x.UpdatedAtUtc).ThenBy(x => x.Id).Skip((filter.Page - 1) * 1000).Take(1001)
            .Select(x => new ReportRow(x.Id, x.AssetCode, x.Name, x.DepartmentId, x.OperationalStatus, x.UpdatedAtUtc, null, null)).ToArrayAsync(ct);
        return new(page.Take(1000).ToArray(), page.Length > 1000, DateTimeOffset.UtcNow, "Current inventory/current department; equipment status is an event-driven local projection and can lag sources.");
    }
}
