namespace MedEquipSLA.Equipment.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class EquipmentAsset
{
    // No public Retire entry point: only the Application assembly may attest locked coordination.
    internal void Retire(EquipmentRetirementIntent intent, VerifiedRetirementEligibility eligibility, DateTimeOffset now)
    {
        if (IsRetired || eligibility.EquipmentId != Id || intent.EquipmentId != Id ||
            eligibility.IntentId != intent.Id || eligibility.Epoch != intent.RetirementEpoch ||
            intent.State != "Ready" || !intent.HasAll("Sealed"))
            throw new InvalidOperationException("Verified retirement eligibility is required.");
        var utc = now.ToUniversalTime();
        intent.Complete(utc);
        IsRetired = true;
        OperationalStatus = "Retired";
        RetiredAtUtc = utc;
        RetiredByUserId = intent.ActorUserId;
        RetirementReason = intent.Reason;
        UpdatedByUserId = intent.ActorUserId;
        UpdatedAtUtc = utc;
        AggregateVersion++;
    }

    public void Transfer(Guid departmentId, Guid actor, DateTimeOffset now)
    {
        if (IsRetired || departmentId == DepartmentId) throw new InvalidOperationException("Invalid equipment transfer.");
        DepartmentId = departmentId;
        UpdatedByUserId = actor;
        UpdatedAtUtc = now;
        AggregateVersion++;
    }

    public bool ApplyBlockingStatus(bool incident, bool maintenance, DateTimeOffset now)
    {
        var status = IsRetired ? "Retired" : incident ? "Broken" : maintenance ? "UnderMaintenance" : "Operational";
        if (OperationalStatus == status) return false;
        OperationalStatus = status;
        UpdatedAtUtc = now;
        AggregateVersion++;
        return true;
    }

    public static EquipmentAsset Register(string code, Guid department, EquipmentProfile profile, Guid actor, DateTimeOffset now)
    {
        var asset = new EquipmentAsset { AssetCode = code, DepartmentId = department, CreatedAtUtc = now, CreatedByUserId = actor };
        asset.SetProfile(profile, actor, now);
        return asset;
    }

    public void UpdateProfile(EquipmentProfile profile, Guid actor, DateTimeOffset now)
    {
        if (IsRetired) throw new InvalidOperationException("Retired equipment cannot be edited.");
        SetProfile(profile, actor, now);
        AggregateVersion++;
    }

    private void SetProfile(EquipmentProfile profile, Guid actor, DateTimeOffset now)
    {
        Name = profile.Name;
        SerialNumber = profile.SerialNumber;
        CategoryId = profile.CategoryId;
        Manufacturer = profile.Manufacturer;
        Model = profile.Model;
        PurchasedOn = profile.PurchasedOn;
        WarrantyUntil = profile.WarrantyUntil;
        UpdatedAtUtc = now;
        UpdatedByUserId = actor;
    }

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
