using MedEquipSLA.Incident.Application.Features.Reports;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Incident.Infrastructure.Persistence;

public sealed class OperationalReportReader(IncidentDbContext db) : IOperationalReportReader
{
    public async Task<ReportPage> ReadAsync(ReportFilter filter, CancellationToken ct)
    {
        var rows = db.Incidents.AsNoTracking();
        if (filter.DepartmentId is { } department) rows = rows.Where(x => x.DepartmentIdSnapshot == department);
        if (filter.Status is { } status) rows = rows.Where(x => x.Status == status);
        if (filter.FromUtc is { } from) rows = rows.Where(x => x.CreatedAtUtc >= from);
        if (filter.UntilUtc is { } until) rows = rows.Where(x => x.CreatedAtUtc < until);
        var page = await rows.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((filter.Page - 1) * 1000).Take(1001)
            .Select(x => new ReportRow(x.Id, x.Code, x.Title, x.DepartmentIdSnapshot, x.Status, x.CreatedAtUtc, x.ResponseOutcome, x.ResolutionOutcome)).ToArrayAsync(ct);
        return new(page.Take(1000).ToArray(), page.Length > 1000, DateTimeOffset.UtcNow, "Incident history by department snapshot; SLA outcomes are final only for Closed incidents.");
    }
}
