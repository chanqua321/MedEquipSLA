namespace MedEquipSLA.Equipment.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class EquipmentAsset
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string AssetCode { get; private set; } = string.Empty;
    public string? SerialNumber { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid CategoryId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? Model { get; private set; }
    public DateOnly? PurchasedOn { get; private set; }
    public DateOnly? WarrantyUntil { get; private set; }
    public bool IsRetired { get; private set; }
    public string OperationalStatus { get; private set; } = "Operational";
    public DateTimeOffset? RetiredAtUtc { get; private set; }
    public Guid? RetiredByUserId { get; private set; }
    public string? RetirementReason { get; private set; }
    public long AggregateVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
