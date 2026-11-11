namespace MedEquipSLA.Equipment.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class Department
{
    public Department() { }

    public static Department Create(string code, string name, Guid actor, DateTimeOffset now) => new()
    {
        Code = code,
        Name = name,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
        CreatedByUserId = actor,
        UpdatedByUserId = actor
    };

    public void Change(string? name, Guid actor, DateTimeOffset now)
    {
        if (name is null)
            IsActive = false;
        else
            Name = name;
        UpdatedAtUtc = now;
        UpdatedByUserId = actor;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
