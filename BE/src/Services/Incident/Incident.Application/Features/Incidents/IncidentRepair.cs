namespace MedEquipSLA.Incident.Application.Features.Incidents;

public sealed record IncidentRepairRow(Guid Id, string Code, string Title, string Status, Guid? AssignedTechnicianId, byte[] RowVersion,
    Guid EquipmentId, string EquipmentNameSnapshot, string ResponseOutcome, string ResolutionOutcome);
public sealed record IncidentAttemptRow(Guid Id, int AttemptNumber, string ResolutionNotes, string ResultSummary, DateTimeOffset CompletedAtUtc,
    string ReviewDecision, string? ReviewReason, bool HasPhoto);
public sealed record IncidentPhoto(byte[] Bytes, string ContentType);
public sealed record IncidentRepairReport(string Notes, string Result, byte[] RowVersion, byte[] Photo, string ContentType);
public sealed record IncidentRepairResult(IncidentWorkflowStatus Status, Guid? AttemptId = null, int? AttemptNumber = null);
public interface IIncidentRepairStore
{
    Task<IReadOnlyList<IncidentRepairRow>> ListAsync(Guid? id, Guid? department, CancellationToken ct);
    Task<IReadOnlyList<IncidentAttemptRow>?> AttemptsAsync(Guid id, Guid? department, CancellationToken ct);
    Task<IncidentPhoto?> PhotoAsync(Guid id, Guid attempt, Guid? department, CancellationToken ct);
    Task<IncidentRepairResult> SubmitAsync(Guid id, Guid actor, bool admin, IncidentRepairReport report, string correlation, CancellationToken ct);
}
public sealed class IncidentRepairQueries(IIncidentRepairStore store)
{
    public Task<IReadOnlyList<IncidentRepairRow>> ListAsync(Guid? id, Guid? department, CancellationToken ct) => store.ListAsync(id, department, ct);
    public Task<IReadOnlyList<IncidentAttemptRow>?> AttemptsAsync(Guid id, Guid? department, CancellationToken ct) => store.AttemptsAsync(id, department, ct);
    public Task<IncidentPhoto?> PhotoAsync(Guid id, Guid attempt, Guid? department, CancellationToken ct) => store.PhotoAsync(id, attempt, department, ct);
}
public sealed class IncidentRepairCommands(IIncidentRepairStore store)
{
    public Task<IncidentRepairResult> HandleAsync(Guid id, Guid actor, bool admin, IncidentRepairReport report, string correlation, CancellationToken ct) =>
        actor == Guid.Empty || report.Notes.Trim().Length is < 1 or > 4000 || report.Result.Trim().Length is < 1 or > 2000 ||
        report.RowVersion.Length != 8 || report.Photo.Length is < 1 or > 5242880 || report.ContentType is not ("image/png" or "image/jpeg")
        ? Task.FromResult(new IncidentRepairResult(IncidentWorkflowStatus.Invalid)) : store.SubmitAsync(id, actor, admin, report, correlation, ct);
}
