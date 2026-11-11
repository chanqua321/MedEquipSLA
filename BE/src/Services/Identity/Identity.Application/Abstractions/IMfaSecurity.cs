namespace MedEquipSLA.Identity.Application.Abstractions;

public sealed record MfaSetupResult(Guid UserId, string SetupToken, string Method, string? Secret, string? AuthenticatorUri);
public interface IMfaSecurity
{
    Task<MfaSetupResult?> SetupAsync(string userName, string password, string method, string? code, string? recoveryCode, CancellationToken ct);
    Task<string[]?> ConfirmAsync(Guid userId, string setupToken, string code, CancellationToken ct);
    Task<bool> EmailCodeAsync(string userName, string password, CancellationToken ct);
    Task<bool> DisableAsync(string userName, string password, string? code, string? recoveryCode, CancellationToken ct);
}
