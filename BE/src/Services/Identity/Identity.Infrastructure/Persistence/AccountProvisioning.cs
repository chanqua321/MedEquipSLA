using System.Net.Mail;
using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Identity.Application.Abstractions;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MedEquipSLA.Identity.Infrastructure.Persistence;

public sealed class AccountProvisioning(IdentityDbContext db, UserManager<ApplicationUser> users, SecurityTickets tickets,
    IConfiguration config) : IAccountProvisioning
{
    public async Task<ProvisionResult> InviteAsync(EmployeeInvitation input, Guid actor, CancellationToken ct)
    {
        var email = input.Email.Trim(); var name = input.FullName.Trim();
        if (actor == Guid.Empty || email.Length > 256 || !MailAddress.TryCreate(email, out var address) || address.Address != email ||
            name.Length is < 1 or > 200 || input.Role is not ("Technician" or "DepartmentStaff") ||
            input.Role == "DepartmentStaff" && input.DepartmentId is not { } || input.DepartmentId == Guid.Empty) return new("Invalid");
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Email lock makes repeated/import-concurrent rows deterministic without a second account.
        var resource = "Invite:" + SecurityTickets.Hash(email.ToUpperInvariant());
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51070,'Invitation lock timeout',1", ct);
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null) return new("Duplicate", existing.Id);
        var user = ApplicationUser.Invite(email, name, input.DepartmentId, input.DepartmentName, actor);
        if (!(await users.CreateAsync(user)).Succeeded || !(await users.AddToRoleAsync(user, input.Role)).Succeeded) return new("Invalid");
        await tickets.CreateAsync(user, "Activation", TimeSpan.FromHours(24), ct);
        db.Add(new AuditEntry(actor, user.Id, "EmployeeInvited", Guid.NewGuid().ToString(), "{}"));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new("Created", user.Id);
    }
    public async Task<ProvisionResult> ResendAsync(Guid id, Guid actor, byte[] version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await tickets.LockAsync(id, ct);
        if (user is null) return new("NotFound");
        if (version.Length != 8 || !version.SequenceEqual(user.RowVersion) || user.IsInvitationSuspended || user.EmailConfirmed || user.PasswordHash is not null ||
            (await users.GetRolesAsync(user)).Contains("Admin")) return new("Conflict");
        user.SetEmployeeAccess(false, actor);
        await tickets.CreateAsync(user, "Activation", TimeSpan.FromHours(24), ct);
        db.Add(new AuditEntry(actor, user.Id, "ActivationResent", Guid.NewGuid().ToString(), "{}"));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new("Created", id);
    }
    public Task<bool> ActivateAsync(Guid user, Guid ticket, string token, string password, CancellationToken ct) =>
        RedeemAsync(user, ticket, token, password, "Activation", ct);
    public Task<bool> ResetAsync(Guid user, Guid ticket, string token, string password, CancellationToken ct) =>
        RedeemAsync(user, ticket, token, password, "PasswordReset", ct);
    private async Task<bool> RedeemAsync(Guid id, Guid ticketId, string token, string password, string kind, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await tickets.LockAsync(id, ct);
        if (user is null || (await users.GetRolesAsync(user)).Contains("Admin")) return false;
        var ticket = await tickets.ReadAsync(id, ticketId, ct);
        if (!SecurityTickets.Valid(ticket, user, kind, token))
        {
            if (ticket is { Used: false, Attempts: < 5 }) { await tickets.SaveAsync(id, ticket with { Attempts = ticket.Attempts + 1 }, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
            return false;
        }
        if (kind == "Activation" && (user.IsInvitationSuspended || user.PasswordHash is not null || user.EmailConfirmed) ||
            kind == "PasswordReset" && (!user.IsActive || !user.EmailConfirmed)) return false;
        foreach (var validator in users.PasswordValidators)
            if (!(await validator.ValidateAsync(users, user, password)).Succeeded) return false;
        user.PasswordHash = users.PasswordHasher.HashPassword(user, password);
        user.SecurityStamp = Guid.NewGuid().ToString();
        if (kind == "Activation") user.Activate(user.Id); else user.SetEmployeeAccess(true, user.Id);
        await tickets.SaveAsync(id, ticket! with { Used = true }, ct);
        await db.RefreshTokens.Where(x => x.UserId == id && x.RevokedAtUtc == null).ExecuteUpdateAsync(s =>
            s.SetProperty(x => x.RevokedAtUtc, DateTimeOffset.UtcNow).SetProperty(x => x.RevocationReason, kind), ct);
        db.Add(new AuditEntry(user.Id, id, kind == "Activation" ? "AccountActivated" : "PasswordReset", ticketId.ToString(), "{}"));
        if (kind == "PasswordReset") db.Add(new OutboxMessage(new(Guid.NewGuid(), UserAccessChangedEvent.Changed, 1, "Identity", id,
            user.AuthorizationVersion, DateTimeOffset.UtcNow, ticketId.ToString(), JsonSerializer.Serialize(new UserAccessChangedEvent(id, user.AuthorizationVersion)))));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
    public async Task RequestResetAsync(string email, CancellationToken ct)
    {
        var found = await users.FindByEmailAsync(email.Trim());
        if (found is null) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await tickets.LockAsync(found.Id, ct);
        if (user is not { IsActive: true, EmailConfirmed: true } || (await users.GetRolesAsync(user)).Contains("Admin")) return;
        await tickets.CreateAsync(user, "PasswordReset", TimeSpan.FromMinutes(30), ct);
        db.Add(new AuditEntry(user.Id, user.Id, "PasswordResetRequested", Guid.NewGuid().ToString(), "{}"));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<SecurityMail?> MailAsync(Guid id, Guid ticketId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user?.Email is null) return null;
        var ticket = await tickets.ReadAsync(id, ticketId, ct);
        if (user.IsInvitationSuspended || ticket is not { Used: false } || ticket.ExpiresAtUtc <= DateTimeOffset.UtcNow || ticket.AuthorizationVersion != user.AuthorizationVersion ||
            ticket.Kind == "Activation" && user.PasswordHash is not null || ticket.Kind != "Activation" && !user.IsActive) return null;
        var baseUrl = config["Identity:PublicWebAddress"]?.TrimEnd('/');
        if (ticket.Kind is "EmailOtp" or "EmailSetup") return new(id, user.Email, "Mã xác minh MedEquipSLA",
            $"Mã xác minh: {tickets.Unprotect(ticket.ProtectedSecret)}\nExpires: {ticket.ExpiresAtUtc:O}");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new InvalidOperationException("Configure Identity:PublicWebAddress before sending account mail.");
        var route = ticket.Kind == "Activation" ? "activate" : "reset-password";
        return new(id, user.Email, ticket.Kind == "Activation" ? "Kích hoạt tài khoản MedEquipSLA" : "Đặt lại mật khẩu MedEquipSLA",
            $"{baseUrl}/{route}?userId={id:D}&ticketId={ticketId:D}&token={tickets.Unprotect(ticket.ProtectedSecret)}\nExpires: {ticket.ExpiresAtUtc:O}");
    }
}
