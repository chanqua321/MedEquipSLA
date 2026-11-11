namespace MedEquipSLA.Contracts.Events;

public sealed record SessionRevokedEvent(Guid UserId, Guid SessionId, string Reason)
{
    public const string Stream = "medequip:identity:sessions:v1";
    public const string Revoked = "UserSessionRevoked";
}
