namespace MedEquipSLA.Incident.Application.Features.Incidents;

public sealed record IncidentSearch(string? Search, string? Status, string? Priority, Guid? EquipmentId,
    Guid? TechnicianId, string Sort, int Page, int PageSize, Guid? DepartmentScope);
public sealed record IncidentRow(Guid Id, string Code, string Title, string Status, string Priority, Guid EquipmentId,
    string EquipmentName, Guid DepartmentId, Guid? AssignedTechnicianId, DateTimeOffset CreatedAtUtc, byte[] RowVersion);
public sealed record IncidentPage(IReadOnlyList<IncidentRow> Items, int Total, int Page, int PageSize);
public sealed record IncidentDetail(IncidentRow Incident, string Description, Guid ReportedByUserId,
    DateTimeOffset ResponseDeadlineAtUtc, DateTimeOffset ResolutionDeadlineAtUtc,
    DateTimeOffset? ClosedAtUtc, string ResponseOutcome, string ResolutionOutcome);
public interface IIncidentReader
{
    Task<IncidentPage> SearchAsync(IncidentSearch query, CancellationToken ct);
    Task<IncidentDetail?> FindAsync(Guid id, Guid? departmentScope, CancellationToken ct);
}
public sealed class IncidentQueries(IIncidentReader reader)
{
    public Task<IncidentPage> SearchAsync(IncidentSearch query, CancellationToken ct) => reader.SearchAsync(query, ct);
    public Task<IncidentDetail?> FindAsync(Guid id, Guid? departmentScope, CancellationToken ct) => reader.FindAsync(id, departmentScope, ct);
}
