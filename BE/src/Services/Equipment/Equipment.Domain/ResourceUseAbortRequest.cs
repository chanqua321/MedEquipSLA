using System.Text.Json;

namespace MedEquipSLA.Equipment.Domain;

public sealed record ResourceUseAbortRequest(Guid EquipmentId, Guid OperationId, string Source, string ResourceType, byte[] RequestHash)
{
    public string CanonicalJson()
    {
        if (EquipmentId == Guid.Empty || OperationId == Guid.Empty || RequestHash.Length != 32 ||
            !(Source == "Maintenance" && ResourceType == "MaintenanceSchedule" || Source == "Incident" && ResourceType == "Incident"))
            throw new InvalidOperationException("Invalid resource-use abort identity.");
        return JsonSerializer.Serialize(new { version = 1, equipmentId = EquipmentId, operationId = OperationId,
            source = Source, resourceType = ResourceType, requestHash = Convert.ToHexString(RequestHash) });
    }
}
