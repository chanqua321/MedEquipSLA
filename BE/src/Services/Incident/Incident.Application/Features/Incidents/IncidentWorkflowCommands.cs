namespace MedEquipSLA.Incident.Application.Features.Incidents;

public sealed record IncidentAssignmentInput(Guid TechnicianId, byte[]? RowVersion);
public sealed record IncidentVersionInput(byte[]? RowVersion);
public sealed record IncidentRejectionInput(string? Reason, byte[]? RowVersion);
public enum IncidentWorkflowStatus { Saved, Invalid, NotFound, Conflict, Forbidden, Unavailable }
public interface IIncidentWorkflowStore
{
    Task<IncidentWorkflowStatus> AssignAsync(Guid id, IncidentAssignmentInput input, Guid actor, long authorizationVersion, string correlation, CancellationToken ct);
    Task<IncidentWorkflowStatus> StartAsync(Guid id, IncidentVersionInput input, Guid actor, string correlation, CancellationToken ct);
    Task<IncidentWorkflowStatus> RejectAsync(Guid id, Guid attemptId, IncidentRejectionInput input, Guid actor, string correlation, CancellationToken ct);
    Task<IncidentWorkflowStatus> AcceptAsync(Guid id, Guid attemptId, IncidentVersionInput input, Guid actor, string correlation, CancellationToken ct);
}
public interface ITechnicianEligibility
{
    // >0 eligible/current version, 0 unavailable, -1 invalid/inactive/non-Technician.
    Task<long> FindVersionAsync(Guid technician, string authorization, CancellationToken ct);
}
public sealed class IncidentWorkflowCommands(IIncidentWorkflowStore store, ITechnicianEligibility technicians)
{
    public async Task<IncidentWorkflowStatus> AssignAsync(Guid id, IncidentAssignmentInput input, Guid actor, string authorization, string correlation, CancellationToken ct)
    {
        if (actor == Guid.Empty || input.TechnicianId == Guid.Empty || input.RowVersion is not { Length: 8 }) return IncidentWorkflowStatus.Invalid;
        var version = await technicians.FindVersionAsync(input.TechnicianId, authorization, ct);
        return version > 0 ? await store.AssignAsync(id, input, actor, version, correlation, ct) : version == -1 ? IncidentWorkflowStatus.Invalid : IncidentWorkflowStatus.Unavailable;
    }
    public Task<IncidentWorkflowStatus> StartAsync(Guid id, IncidentVersionInput input, Guid actor, string correlation, CancellationToken ct) =>
        actor == Guid.Empty || input.RowVersion is not { Length: 8 } ? Task.FromResult(IncidentWorkflowStatus.Invalid) : store.StartAsync(id, input, actor, correlation, ct);
    public Task<IncidentWorkflowStatus> RejectAsync(Guid id, Guid attempt, IncidentRejectionInput input, Guid actor, string correlation, CancellationToken ct) =>
        actor == Guid.Empty || input.RowVersion is not { Length: 8 } || input.Reason?.Trim().Length is not (> 0 and <= 2000)
        ? Task.FromResult(IncidentWorkflowStatus.Invalid) : store.RejectAsync(id, attempt, input, actor, correlation, ct);
    public Task<IncidentWorkflowStatus> AcceptAsync(Guid id, Guid attempt, IncidentVersionInput input, Guid actor, string correlation, CancellationToken ct) =>
        actor == Guid.Empty || input.RowVersion is not { Length: 8 } ? Task.FromResult(IncidentWorkflowStatus.Invalid) : store.AcceptAsync(id, attempt, input, actor, correlation, ct);
}
