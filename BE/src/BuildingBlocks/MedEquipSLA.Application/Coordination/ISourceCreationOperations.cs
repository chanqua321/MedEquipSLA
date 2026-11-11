namespace MedEquipSLA.Application.Coordination;

public sealed record SourceOperationView(Guid OperationId, Guid? EquipmentId, string State, int AttemptCount,
    long TotalAttemptCount, bool InterventionRequired, bool CancellationRequested, string? LastError, byte[] RowVersion);
public sealed record SourceOperationCommand(string Action, Guid Actor, Guid CommandId, byte[] RowVersion, string Reason);
public interface ISourceCreationOperations
{
    Task<SourceOperationView?> FindAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SourceOperationView>> ListAsync(Guid? equipment, int page, CancellationToken ct);
    Task<(string Status, SourceOperationView? Value)> ExecuteAsync(Guid id, SourceOperationCommand input, CancellationToken ct);
    Task<int> ReconcileCancellationsAsync(CancellationToken ct);
}
