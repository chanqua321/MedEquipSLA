namespace MedEquipSLA.Maintenance.Application.Features.Reports;

public sealed record ReportFilter(Guid? DepartmentId, string? Status, DateTimeOffset? FromUtc, DateTimeOffset? UntilUtc, int Page);
public sealed record ReportRow(Guid Id, string Code, string Name, Guid DepartmentId, string Status, DateTimeOffset AtUtc,
    string? ResponseOutcome, string? ResolutionOutcome);
public sealed record ReportPage(IReadOnlyList<ReportRow> Items, bool HasNext, DateTimeOffset GeneratedAtUtc, string DataScope);
public interface IOperationalReportReader { Task<ReportPage> ReadAsync(ReportFilter filter, CancellationToken ct); }
public sealed class OperationalReportQuery(IOperationalReportReader reader)
{
    public Task<ReportPage> HandleAsync(ReportFilter filter, CancellationToken ct)
    {
        if (filter.Page is < 1 or > 1_000_000 || filter.Status?.Length > 30 || filter.UntilUtc <= filter.FromUtc)
            throw new ArgumentException("Invalid report filters.");
        return reader.ReadAsync(filter, ct);
    }
}
