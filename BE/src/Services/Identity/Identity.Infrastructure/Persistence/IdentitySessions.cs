using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using MedEquipSLA.Identity.Application.Abstractions;
using MedEquipSLA.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Identity.Infrastructure.Persistence;

public sealed class IdentitySessions(IdentityDbContext db, UserManager<ApplicationUser> users, MfaSecurity mfa, SecurityTickets tickets) : IIdentitySessions
{
    public async Task<SessionGrant?> LoginAsync(string userName, string password, CancellationToken ct, string? code = null, string? recoveryCode = null)
    {
        var found = await users.FindByNameAsync(userName);
        if (found is null) return null;
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await tickets.LockAsync(found.Id, ct);
        if (user is null || !user.IsActive || !user.EmailConfirmed || await users.IsLockedOutAsync(user))
            return null;
        if (!await users.CheckPasswordAsync(user, password) || !await mfa.VerifyAsync(user, code, recoveryCode, ct))
        {
            var failedAt = DateTimeOffset.UtcNow;
            await db.Users.Where(x => x.Id == user.Id && x.IsActive && x.LockoutEnabled &&
                    (x.LockoutEnd == null || x.LockoutEnd <= failedAt))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(x => x.LockoutEnd, x => x.AccessFailedCount < 4 ? x.LockoutEnd :
                        failedAt.AddMinutes(x.AccessFailedCount == 4 ? 15 :
                            x.AccessFailedCount == 5 ? 30 : x.AccessFailedCount == 6 ? 60 : 120))
                    .SetProperty(x => x.AccessFailedCount, x => x.AccessFailedCount >= 8 ? 8 : x.AccessFailedCount + 1), ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return null;
        }

        var reset = await users.ResetAccessFailedCountAsync(user);
        if (!reset.Succeeded || !user.IsActive || await users.IsLockedOutAsync(user))
            return null;
        var now = DateTimeOffset.UtcNow;
        var (grant, token) = await CreateAsync(user, Guid.NewGuid(), now);
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return grant;
    }

    public async Task<SessionGrant?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (current is null || current.ExpiresAtUtc <= now)
            return null;
        if (current.RevokedAtUtc is not null)
        {
            if (current.ReplacedByTokenId is not null)
            {
                await db.RefreshTokens.Where(x => x.FamilyId == current.FamilyId && x.UserId == current.UserId && x.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAtUtc, now)
                        .SetProperty(x => x.RevocationReason, "Reuse"), ct);
                var version = await db.Users.Where(x => x.Id == current.UserId).Select(x => x.AuthorizationVersion).SingleAsync(ct);
                AddSessionEvent(current.UserId, current.FamilyId, version, "RefreshReuse", now);
                db.Add(AuditEntry.ForSecurityService(current.UserId, "RefreshTokenReuseDetected", JsonSerializer.Serialize(new { sessionId = current.FamilyId })));
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            return null;
        }

        var user = await users.FindByIdAsync(current.UserId.ToString());
        if (user is null || !user.IsActive || !user.EmailConfirmed || await users.IsLockedOutAsync(user) ||
            mfa.Enabled && (await users.GetRolesAsync(user)).Contains("Admin") && !user.TwoFactorEnabled)
            return null;
        var (grant, next) = await CreateAsync(user, current.FamilyId, now, current.ExpiresAtUtc);
        // Claim the old token first so concurrent rotations cannot both mint a successor.
        var updated = await db.RefreshTokens.Where(x => x.Id == current.Id && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAtUtc, now)
                .SetProperty(x => x.RevocationReason, "Rotated"), ct);
        if (updated != 1)
            return null;
        db.RefreshTokens.Add(next);
        await db.SaveChangesAsync(ct);
        await db.RefreshTokens.Where(x => x.Id == current.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.ReplacedByTokenId, next.Id), ct);
        await transaction.CommitAsync(ct);
        return grant;
    }

    public async Task<bool> IsCurrentAsync(SessionUser claims, CancellationToken ct)
    {
        var current = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == claims.Id, ct);
        // Match login/refresh while MFA is paused; preserve authorization version, role and session validation.
        return current is { IsActive: true, EmailConfirmed: true } && (!mfa.Enabled || !claims.Roles.Contains("Admin") || current.TwoFactorEnabled) && current.AuthorizationVersion == claims.AuthorizationVersion &&
            current.DepartmentId == claims.DepartmentId && !await users.IsLockedOutAsync(current) &&
            (await users.GetRolesAsync(current)).ToHashSet(StringComparer.Ordinal).SetEquals(claims.Roles) &&
            await db.RefreshTokens.AnyAsync(x => x.UserId == claims.Id && x.FamilyId == claims.SessionId &&
                x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTimeOffset.UtcNow, ct);
    }

    public async Task RevokeAsync(SessionUser claims, bool all, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (!await IsCurrentAsync(claims, ct)) return;
        var now = DateTimeOffset.UtcNow;
        await db.RefreshTokens.Where(x => x.UserId == claims.Id && (all || x.FamilyId == claims.SessionId) && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAtUtc, now)
                .SetProperty(x => x.RevocationReason, "Logout"), ct);
        if (all)
        {
            await db.Users.Where(x => x.Id == claims.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.AuthorizationVersion, x => x.AuthorizationVersion + 1), ct);
            db.Add(new OutboxMessage(new(Guid.NewGuid(), UserAccessChangedEvent.Changed, 1, "Identity", claims.Id,
                claims.AuthorizationVersion + 1, now, Guid.NewGuid().ToString(), JsonSerializer.Serialize(new UserAccessChangedEvent(claims.Id, claims.AuthorizationVersion + 1)))));
        }
        else AddSessionEvent(claims.Id, claims.SessionId, claims.AuthorizationVersion, "Logout", now);
        db.Add(new AuditEntry(claims.Id, claims.Id, all ? "AllSessionsRevoked" : "SessionRevoked", Guid.NewGuid().ToString(),
            JsonSerializer.Serialize(new { claims.SessionId })));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<(SessionGrant Grant, RefreshToken Token)> CreateAsync(ApplicationUser user, Guid family, DateTimeOffset now,
        DateTimeOffset? fixedExpiry = null)
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiration = fixedExpiry ?? now.AddDays(7);
        var roles = (await users.GetRolesAsync(user)).ToArray();
        return (new(new(user.Id, user.DepartmentId, user.AuthorizationVersion, roles, family), raw, expiration),
            new RefreshToken(user.Id, family, Hash(raw), now, expiration));
    }

    private static byte[] Hash(string raw) => SHA256.HashData(Encoding.UTF8.GetBytes(raw));
    private void AddSessionEvent(Guid user, Guid session, long version, string reason, DateTimeOffset now) =>
        db.Add(new OutboxMessage(new(Guid.NewGuid(), SessionRevokedEvent.Revoked, 1, "Identity", user, version, now,
            Guid.NewGuid().ToString(), JsonSerializer.Serialize(new SessionRevokedEvent(user, session, reason)))));
}
