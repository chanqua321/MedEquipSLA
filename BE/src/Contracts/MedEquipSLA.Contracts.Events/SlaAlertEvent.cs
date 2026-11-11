namespace MedEquipSLA.Contracts.Events;

public sealed record SlaAlertEvent(Guid RecipientUserId, Guid DepartmentId, DateTimeOffset DeadlineAtUtc)
{
    public const string Stream = "medequip:incident:sla:v1";
    public const string ResponseBreached = "SlaResponseBreached";
    public const string ResolutionBreached = "SlaResolutionBreached";
}
