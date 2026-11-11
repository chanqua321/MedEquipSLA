namespace MedEquipSLA.Identity.Application.Abstractions;

public sealed record ActiveRecipientEmail(Guid Id, string Email);
public sealed record TechnicianAccess(Guid UserId, bool Eligible, long AuthorizationVersion);
public interface IIdentityRecipientDirectory
{
    Task<Guid[]> RecipientsAsync(Guid? technician, CancellationToken ct);
    Task<ActiveRecipientEmail?> EmailAsync(Guid id, CancellationToken ct);
    Task<TechnicianAccess> TechnicianAsync(Guid id, CancellationToken ct);
}
