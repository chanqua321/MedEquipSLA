namespace MedEquipSLA.Identity.Application.Abstractions;

public sealed record EmployeeInvitation(string Email, string FullName, string Role, Guid? DepartmentId, string? DepartmentName);
public sealed record ProvisionResult(string Status, Guid? UserId = null);
public sealed record SecurityMail(Guid UserId, string Address, string Subject, string Body);
public interface IAccountProvisioning
{
    Task<ProvisionResult> InviteAsync(EmployeeInvitation input, Guid actor, CancellationToken ct);
    Task<ProvisionResult> ResendAsync(Guid user, Guid actor, byte[] version, CancellationToken ct);
    Task<bool> ActivateAsync(Guid user, Guid ticket, string token, string password, CancellationToken ct);
    Task<SecurityMail?> MailAsync(Guid user, Guid ticket, CancellationToken ct);
    Task RequestResetAsync(string email, CancellationToken ct);
    Task<bool> ResetAsync(Guid user, Guid ticket, string token, string password, CancellationToken ct);
}
