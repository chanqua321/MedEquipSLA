namespace MedEquipSLA.Incident.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class SlaPolicy
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Priority { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
