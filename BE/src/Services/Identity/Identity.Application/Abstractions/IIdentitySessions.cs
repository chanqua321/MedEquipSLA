namespace MedEquipSLA.Identity.Application.Abstractions;

public sealed record SessionUser(Guid Id, Guid? DepartmentId, long AuthorizationVersion, IReadOnlyList<string> Roles, Guid SessionId);
public sealed record SessionGrant(SessionUser User, string RefreshToken, DateTimeOffset RefreshExpiresAtUtc);

public interface IIdentitySessions
{
    Task<SessionGrant?> LoginAsync(string userName, string password, CancellationToken ct, string? code = null, string? recoveryCode = null);
    Task<SessionGrant?> RefreshAsync(string refreshToken, CancellationToken ct);
    Task<bool> IsCurrentAsync(SessionUser claims, CancellationToken ct);
    Task RevokeAsync(SessionUser claims, bool all, CancellationToken ct);
}
