namespace MedEquipSLA.Equipment.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class EquipmentUseRegistration
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EquipmentId { get; private set; }
    public string SourceService { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public Guid SourceOperationId { get; private set; }
    public string State { get; private set; } = string.Empty;
    public long SourceVersion { get; private set; }
    public DateTimeOffset RegisteredAtUtc { get; private set; }
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public DateTimeOffset? ReleasedAtUtc { get; private set; }
    public DateTimeOffset? LastVerifiedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
