using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using MedEquipSLA.Identity.Application.Abstractions;
using MedEquipSLA.Contracts.Events;
using Microsoft.Extensions.Configuration;

namespace MedEquipSLA.Identity.Infrastructure.Persistence;

public sealed class MfaSecurity(IdentityDbContext db, UserManager<ApplicationUser> users, SecurityTickets tickets, IConfiguration? configuration = null) : IMfaSecurity
{
    public const string ConfigurationKey = "Authentication:MfaEnabled";
    // Owner-requested pause: appsettings sets false. Set true and restart Identity to restore MFA.
    // Keep TOTP, recovery codes and the original verification flow intact; missing config stays secure.
    public bool Enabled => configuration?.GetValue(ConfigurationKey, true) ?? true;
    private const string Provider = "MedEquipSLA.Mfa.v1";
    private sealed record Settings(string Method, string Secret, long LastStep, string[] RecoveryHashes, Guid? EmailTicket = null);
    private sealed record Pending(string TokenHash, string Method, string Secret, DateTimeOffset Expires, long Version, int Attempts = 0, Guid? EmailTicket = null);
    private async Task<IdentityUserToken<Guid>?> RowAsync(Guid user, string name, CancellationToken ct) =>
        await db.UserTokens.SingleOrDefaultAsync(x => x.UserId == user && x.LoginProvider == Provider && x.Name == name, ct);
    private async Task<T?> ReadAsync<T>(Guid user, string name, CancellationToken ct)
    {
        var row = await RowAsync(user, name, ct);
        return row?.Value is { } value ? JsonSerializer.Deserialize<T>(tickets.Unprotect(value)) : default;
    }
    private async Task WriteAsync<T>(Guid user, string name, T value, CancellationToken ct)
    {
        var row = await RowAsync(user, name, ct);
        if (row is null) { row = new() { UserId = user, LoginProvider = Provider, Name = name }; db.Add(row); }
        row.Value = tickets.Protect(JsonSerializer.Serialize(value));
    }
    public async Task<bool> VerifyAsync(ApplicationUser user, string? code, string? recovery, CancellationToken ct)
    {
        if (!Enabled) return true; // Password, account state, lockout and session checks still run in IdentitySessions.
        var settings = await ReadAsync<Settings>(user.Id, "Settings", ct);
        var admin = (await users.GetRolesAsync(user)).Contains("Admin");
        if (settings is null) return !admin && !user.TwoFactorEnabled;
        if (!string.IsNullOrEmpty(recovery))
        {
            var hash = SecurityTickets.Hash(recovery.Trim().ToUpperInvariant());
            if (!settings.RecoveryHashes.Contains(hash, StringComparer.Ordinal)) return false;
            await WriteAsync(user.Id, "Settings", settings with { RecoveryHashes = settings.RecoveryHashes.Where(x => x != hash).ToArray() }, ct);
            db.Add(new AuditEntry(user.Id, user.Id, "MfaRecoveryCodeUsed", Guid.NewGuid().ToString(), "{}"));
            return true;
        }
        if (code is not { Length: 6 } || !code.All(char.IsAsciiDigit)) return false;
        if (settings.Method == "Totp")
        {
            var step = MatchStep(settings.Secret, code, DateTimeOffset.UtcNow, settings.LastStep);
            if (step is null) return false;
            await WriteAsync(user.Id, "Settings", settings with { LastStep = step.Value }, ct); return true;
        }
        if (admin || settings.EmailTicket is not { } id) return false;
        var ticket = await tickets.ReadAsync(user.Id, id, ct);
        if (!SecurityTickets.Valid(ticket, user, "EmailOtp", code))
        {
            if (ticket is { Used: false, Attempts: < 5 }) await tickets.SaveAsync(user.Id, ticket with { Attempts = ticket.Attempts + 1 }, ct);
            return false;
        }
        await tickets.SaveAsync(user.Id, ticket! with { Used = true }, ct); return true;
    }
    public async Task<MfaSetupResult?> SetupAsync(string name, string password, string method, string? code, string? recovery, CancellationToken ct)
    {
        if (!Enabled) return null;
        if (method is not ("Totp" or "Email")) return null;
        var found = await users.FindByNameAsync(name);
        if (found is null) return null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await tickets.LockAsync(found.Id, ct);
        if (user is not { IsActive: true } || await users.IsLockedOutAsync(user)) return null;
        if (!await users.CheckPasswordAsync(user, password)) { await FailureAsync(user, ct); await tx.CommitAsync(ct); return null; }
        var admin = (await users.GetRolesAsync(user)).Contains("Admin");
        if (admin && method != "Totp") return null;
        if (user.TwoFactorEnabled && !await VerifyAsync(user, code, recovery, ct)) { await FailureAsync(user, ct); await tx.CommitAsync(ct); return null; }
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var secret = Base32(RandomNumberGenerator.GetBytes(20));
        Guid? emailTicket = null;
        if (method == "Email") emailTicket = (await tickets.CreateAsync(user, "EmailSetup", TimeSpan.FromMinutes(5), ct,
            RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"))).Id;
        await WriteAsync(user.Id, "Pending", new Pending(SecurityTickets.Hash(raw), method, secret, DateTimeOffset.UtcNow.AddMinutes(5), user.AuthorizationVersion, EmailTicket: emailTicket), ct);
        db.Add(new AuditEntry(user.Id, user.Id, "MfaSetupRequested", Guid.NewGuid().ToString(), "{}"));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(user.Id, raw, method, method == "Totp" ? secret : null, method == "Totp"
            ? $"otpauth://totp/MedEquipSLA:{Uri.EscapeDataString(user.UserName!)}?secret={secret}&issuer=MedEquipSLA&algorithm=SHA1&digits=6&period=30" : null);
    }
    public async Task<string[]?> ConfirmAsync(Guid userId, string token, string code, CancellationToken ct)
    {
        if (!Enabled) return null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await tickets.LockAsync(userId, ct);
        var pending = await ReadAsync<Pending>(userId, "Pending", ct);
        if (user is not { IsActive: true } || pending is null || pending.Expires <= DateTimeOffset.UtcNow || pending.Attempts >= 5 ||
            pending.Version != user.AuthorizationVersion || !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(pending.TokenHash), Encoding.ASCII.GetBytes(SecurityTickets.Hash(token)))) return null;
        long? step = pending.Method == "Totp" ? MatchStep(pending.Secret, code, DateTimeOffset.UtcNow, -1) : null;
        var email = pending.EmailTicket is { } id ? await tickets.ReadAsync(userId, id, ct) : null;
        var valid = pending.Method == "Totp" ? step is not null : SecurityTickets.Valid(email, user, "EmailSetup", code);
        if (!valid)
        {
            await WriteAsync(userId, "Pending", pending with { Attempts = pending.Attempts + 1 }, ct);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return null;
        }
        var recovery = Enumerable.Range(0, 10).Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(10))).ToArray();
        await WriteAsync(userId, "Settings", new Settings(pending.Method, pending.Secret, step ?? -1, recovery.Select(SecurityTickets.Hash).ToArray()), ct);
        db.Remove((await RowAsync(userId, "Pending", ct))!);
        if (email is not null) await tickets.SaveAsync(userId, email with { Used = true }, ct);
        user.TwoFactorEnabled = true; user.SetEmployeeAccess(true, userId);
        await RevokeAsync(userId, ct);
        db.Add(new AuditEntry(userId, userId, "MfaEnabled", Guid.NewGuid().ToString(), "{}"));
        AddAccessEvent(user);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return recovery;
    }
    public async Task<bool> EmailCodeAsync(string name, string password, CancellationToken ct)
    {
        if (!Enabled) return false;
        var found = await users.FindByNameAsync(name); if (found is null) return false;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await tickets.LockAsync(found.Id, ct);
        if (user is not { IsActive: true, TwoFactorEnabled: true } || await users.IsLockedOutAsync(user) ||
            (await users.GetRolesAsync(user)).Contains("Admin")) return false;
        if (!await users.CheckPasswordAsync(user, password)) { await FailureAsync(user, ct); await tx.CommitAsync(ct); return false; }
        var settings = await ReadAsync<Settings>(user.Id, "Settings", ct);
        if (settings?.Method != "Email") return false;
        if (settings.EmailTicket is { } previous && await tickets.ReadAsync(user.Id, previous, ct) is { Used: false } recent &&
            recent.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(4)) return false;
        var ticket = await tickets.CreateAsync(user, "EmailOtp", TimeSpan.FromMinutes(5), ct,
            RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"));
        await WriteAsync(user.Id, "Settings", settings with { EmailTicket = ticket.Id }, ct);
        db.Add(new AuditEntry(user.Id, user.Id, "MfaEmailRequested", ticket.Id.ToString(), "{}"));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
    public async Task<bool> DisableAsync(string name, string password, string? code, string? recovery, CancellationToken ct)
    {
        if (!Enabled) return false;
        var found = await users.FindByNameAsync(name); if (found is null) return false;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await tickets.LockAsync(found.Id, ct);
        if (user is not { IsActive: true, TwoFactorEnabled: true } || await users.IsLockedOutAsync(user) ||
            (await users.GetRolesAsync(user)).Contains("Admin")) return false;
        if (!await users.CheckPasswordAsync(user, password) || !await VerifyAsync(user, code, recovery, ct))
        { await FailureAsync(user, ct); await tx.CommitAsync(ct); return false; }
        db.Remove((await RowAsync(user.Id, "Settings", ct))!);
        if (await RowAsync(user.Id, "Pending", ct) is { } pending) db.Remove(pending);
        user.TwoFactorEnabled = false; user.SetEmployeeAccess(true, user.Id); await RevokeAsync(user.Id, ct);
        db.Add(new AuditEntry(user.Id, user.Id, "MfaDisabled", Guid.NewGuid().ToString(), "{}"));
        AddAccessEvent(user);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
    private Task<int> RevokeAsync(Guid user, CancellationToken ct) => db.RefreshTokens.Where(x => x.UserId == user && x.RevokedAtUtc == null)
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTimeOffset.UtcNow).SetProperty(x => x.RevocationReason, "MfaChanged"), ct);
    private async Task FailureAsync(ApplicationUser user, CancellationToken ct)
    {
        user.AccessFailedCount = Math.Min(8, user.AccessFailedCount + 1);
        if (user.AccessFailedCount >= 5) user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(user.AccessFailedCount switch { 5 => 15, 6 => 30, 7 => 60, _ => 120 });
        db.Add(AuditEntry.ForSecurityService(user.Id, "MfaVerificationFailed", "{}"));
        await db.SaveChangesAsync(ct);
    }
    private void AddAccessEvent(ApplicationUser user) => db.Add(new OutboxMessage(new(Guid.NewGuid(), UserAccessChangedEvent.Changed,
        1, "Identity", user.Id, user.AuthorizationVersion, DateTimeOffset.UtcNow, Guid.NewGuid().ToString(),
        JsonSerializer.Serialize(new UserAccessChangedEvent(user.Id, user.AuthorizationVersion)))));
    public static long? MatchStep(string secret, string code, DateTimeOffset now, long lastStep)
    {
        if (code.Length != 6 || !code.All(char.IsAsciiDigit)) return null;
        var current = now.ToUnixTimeSeconds() / 30;
        for (var step = current - 1; step <= current + 1; step++)
            if (step > lastStep && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Totp(secret, step)), Encoding.ASCII.GetBytes(code))) return step;
        return null;
    }
    public static string Totp(string secret, long step)
    {
        var bytes = new List<byte>(); int buffer = 0, bits = 0;
        foreach (var c in secret) { var n = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".IndexOf(c); if (n < 0) throw new FormatException(); buffer = (buffer << 5) | n; bits += 5; if (bits >= 8) { bits -= 8; bytes.Add((byte)(buffer >> bits)); } }
        Span<byte> counter = stackalloc byte[8]; BinaryPrimitives.WriteInt64BigEndian(counter, step);
        var hash = HMACSHA1.HashData(bytes.ToArray(), counter); var offset = hash[^1] & 15;
        var binary = BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(offset, 4)) & 0x7fffffff;
        return (binary % 1_000_000).ToString("D6");
    }
    private static string Base32(byte[] bytes)
    {
        var output = new StringBuilder(); int buffer = 0, bits = 0;
        foreach (var value in bytes) { buffer = (buffer << 8) | value; bits += 8; while (bits >= 5) { bits -= 5; output.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"[(buffer >> bits) & 31]); } }
        if (bits > 0) output.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"[(buffer << (5 - bits)) & 31]); return output.ToString();
    }
}
