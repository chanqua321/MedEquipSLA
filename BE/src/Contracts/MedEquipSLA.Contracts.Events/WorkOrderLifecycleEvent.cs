namespace MedEquipSLA.Contracts.Events;

public sealed record WorkOrderLifecycleEvent(Guid EquipmentId, Guid RegistrationId, Guid OperationId,
    long FencingToken, string RegistrationPayloadJson, string Status, bool IsBlocking)
{
    public const string Stream = "medequip:workorder:lifecycle:v1";
    public const string Created = "WorkOrderCreated";
    public const string Updated = "WorkOrderUpdated";
}
