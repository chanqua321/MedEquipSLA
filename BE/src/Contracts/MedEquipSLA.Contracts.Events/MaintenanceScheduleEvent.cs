namespace MedEquipSLA.Contracts.Events;

public sealed record MaintenanceScheduleEvent(Guid EquipmentId, Guid RegistrationId, bool IsActive, bool IsEnded = false)
{
    public const string Stream = "medequip:maintenance:schedules:v1";
    public const string Created = "MaintenanceScheduleCreated";
    public const string Updated = "MaintenanceScheduleUpdated";
    public const string Deactivated = "MaintenanceScheduleDeactivated";
    public const string Ended = "MaintenanceScheduleEnded";
}
