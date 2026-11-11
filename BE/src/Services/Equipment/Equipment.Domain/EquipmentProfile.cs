namespace MedEquipSLA.Equipment.Domain;

public sealed record EquipmentProfile(string Name, string? SerialNumber, Guid CategoryId, string? Manufacturer,
    string? Model, DateOnly? PurchasedOn, DateOnly? WarrantyUntil);
