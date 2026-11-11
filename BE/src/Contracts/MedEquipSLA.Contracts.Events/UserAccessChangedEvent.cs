namespace MedEquipSLA.Contracts.Events;

public sealed record UserAccessChangedEvent(Guid UserId, long AuthorizationVersion)
{
    public const string Stream = "medequip:identity:access:v1";
    public const string Changed = "UserAccessChanged";
}
