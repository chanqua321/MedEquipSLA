namespace MedEquipSLA.Contracts.Events;

public sealed record IncidentLifecycleEvent(Guid EquipmentId, Guid RegistrationId, string Status)
{
    public const string Stream = "medequip:incident:lifecycle:v1";
    public const string Created = "IncidentCreated";
    public const string Closed = "IncidentClosed";
}
