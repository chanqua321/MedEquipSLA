namespace MedEquipSLA.Contracts.Events;

public sealed record UserNotificationEvent(string Kind, string Audience, Guid? RecipientUserId,
    string ResourceType, Guid ResourceId, Guid? DepartmentId, string Title, string Body)
{
    public const string Stream = "medequip:notifications:v1";
    public const string Requested = "UserNotificationRequested";
    public IntegrationEvent Envelope(string producer, long version, DateTimeOffset now, string correlation) =>
        new(Guid.NewGuid(), Requested, 1, producer, ResourceId, version, now, correlation,
            System.Text.Json.JsonSerializer.Serialize(this));
    public static readonly string[] Kinds = ["IncidentCreated", "IncidentAssigned", "IncidentResolved", "IncidentClosed",
        "SlaResponseBreached", "SlaResolutionBreached", "MaintenanceDueSoon", "MaintenanceOverdue", "WorkOrderAssigned", "WorkOrderCompleted"];
}
