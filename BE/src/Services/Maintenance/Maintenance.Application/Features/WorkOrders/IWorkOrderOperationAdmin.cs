namespace MedEquipSLA.Maintenance.Application.Features.WorkOrders;

public sealed record WorkOrderOperationView(Guid OperationId, Guid? EquipmentId, Guid? OccurrenceId, Guid? ResourceId,
    string State, int AttemptCount, long TotalAttemptCount, bool InterventionRequired, DateTimeOffset? NextRetryAtUtc,
    string? LastError, bool CancellationRequested, byte[] RowVersion);
public enum OperationCommandStatus { Saved, NotFound, Conflict }
public sealed record OperationCommandResult(OperationCommandStatus Status, WorkOrderOperationView? Value = null);
public interface IWorkOrderOperationAdmin
{
    Task<IReadOnlyList<WorkOrderOperationView>> ListAsync(Guid? equipment, string? state, int page, CancellationToken ct);
    Task<WorkOrderOperationView?> FindAsync(Guid operation, CancellationToken ct);
    Task<OperationCommandResult> ExecuteAsync(Guid operation, string action, Guid actor, Guid command, byte[] version,
        string reason, CancellationToken ct);
}
