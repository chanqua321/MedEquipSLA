namespace MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration;

public sealed record ReconciliationView(string SourceService, Guid? SnapshotId, string? Watermark,
    string? Cursor, string State, DateTimeOffset? CapturedAtUtc, DateTimeOffset? CompletedAtUtc, string? LastError);
public interface IReconciliationReader
{
    Task<IReadOnlyList<ReconciliationView>> ListAsync(CancellationToken ct);
}
public sealed class ReconciliationQuery(IReconciliationReader reader)
{
    public Task<IReadOnlyList<ReconciliationView>> ExecuteAsync(CancellationToken ct) => reader.ListAsync(ct);
}
