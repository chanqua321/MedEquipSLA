namespace MedEquipSLA.Incident.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class SlaPolicy
{
    public static SlaPolicy Create(string priority, Guid actor, DateTimeOffset now) => new()
    {
        Priority = priority, Name = priority, CreatedAtUtc = now, UpdatedAtUtc = now,
        CreatedByUserId = actor, UpdatedByUserId = actor
    };

    public void Configure(bool enabled, Guid actor, DateTimeOffset now)
    {
        IsEnabled = enabled;
        UpdatedByUserId = actor;
        UpdatedAtUtc = now;
    }

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
