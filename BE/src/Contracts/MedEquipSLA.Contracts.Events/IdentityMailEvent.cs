namespace MedEquipSLA.Contracts.Events;

// No activation link, OTP, password or recovery code travels through Redis.
public sealed record IdentityMailEvent(Guid UserId, Guid TicketId)
{
    public const string Stream = "medequip:identity:mail:v1";
    public const string Requested = "IdentitySecurityMailRequested";
}
