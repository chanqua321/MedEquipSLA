namespace MedEquipSLA.Equipment.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class EquipmentCategory
{
    public void Revise(string name, string? description, bool active, Guid actor, DateTimeOffset now)
    {
        if (name.Trim().Length is < 1 or > 200 || description?.Length > 1000 || actor == Guid.Empty)
            throw new InvalidOperationException("Valid category name and actor required.");
        Name = name.Trim(); Description = description; IsActive = active; UpdatedByUserId = actor; UpdatedAtUtc = now.ToUniversalTime();
    }
    public static EquipmentCategory Create(string code, string name, Guid actor, DateTimeOffset now) =>
        new() { Code = code, Name = name, CreatedAtUtc = now, UpdatedAtUtc = now, CreatedByUserId = actor, UpdatedByUserId = actor };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
