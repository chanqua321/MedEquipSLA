namespace MedEquipSLA.Contracts.Events;

public sealed record EquipmentDepartmentChanged(Guid FromDepartmentId, string FromDepartmentName,
    Guid ToDepartmentId, string ToDepartmentName)
{
    public const string EventType = "EquipmentDepartmentChanged";
    public const string Stream = "medequip:equipment:lifecycle:v1";
}
