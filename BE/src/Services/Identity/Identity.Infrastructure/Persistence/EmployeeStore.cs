using System.Data;
using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Identity.Application.Abstractions;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Identity.Infrastructure.Persistence;

public sealed class EmployeeStore(IdentityDbContext db, UserManager<ApplicationUser> manager, IIdentitySessions sessions, SecurityTickets tickets) : IEmployeeStore
{
    public async Task<long?> TechnicianVersionAsync(Guid id, CancellationToken ct) =>
        await (from user in db.Users.AsNoTracking() join link in db.UserRoles on user.Id equals link.UserId
            join role in db.Roles on link.RoleId equals role.Id
            where user.Id == id && user.IsActive && user.EmailConfirmed && user.PasswordHash != null && role.Name == "Technician"
            select (long?)user.AuthorizationVersion).SingleOrDefaultAsync(ct);
    public async Task<EmployeePage> ListAsync(string? term, int currentPage, CancellationToken ct)
    {
            var query = from user in db.Users.AsNoTracking()
                        join link in db.UserRoles on user.Id equals link.UserId
                        join role in db.Roles on link.RoleId equals role.Id
                        where role.Name == "Technician" || role.Name == "DepartmentStaff"
                        select new { user.Id, Name = user.FullName, user.Email, Role = role.Name!, user.DepartmentId,
                            user.IsActive, user.EmailConfirmed, user.RowVersion, user.IsInvitationSuspended };
            if (!string.IsNullOrEmpty(term))
                query = query.Where(x => x.Name.Contains(term) || (x.Email != null && x.Email.Contains(term)));

            var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
                .Skip((currentPage - 1) * 20).Take(21)
                .Select(x => new EmployeeView(x.Id, x.Name, x.Email, x.Role, x.DepartmentId,
                    x.IsActive, x.EmailConfirmed, x.RowVersion, x.IsInvitationSuspended)).ToListAsync(ct);
            return new(rows.Take(20).ToArray(), currentPage, rows.Count > 20);
    }
    public async Task<EmployeeChangeStatus> ChangeAccessAsync(Guid id, EmployeeAccessInput input, SessionUser actor, string correlation, CancellationToken ct)
    {
        var active = input.Active!.Value;
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await sessions.IsCurrentAsync(actor, ct)) return EmployeeChangeStatus.Unauthorized;
            var user = await tickets.LockAsync(id, ct);
            if (user is null) return EmployeeChangeStatus.NotFound;
            var roles = await manager.GetRolesAsync(user);
            if (roles.Count != 1 || roles[0] is not ("Technician" or "DepartmentStaff")) return EmployeeChangeStatus.Forbidden;
            if (!user.RowVersion.SequenceEqual(input.RowVersion)) return EmployeeChangeStatus.Conflict;
            var pendingActivation = !user.EmailConfirmed && !await manager.HasPasswordAsync(user);
            if (active && (!user.EmailConfirmed || !await manager.HasPasswordAsync(user)) && !user.IsInvitationSuspended)
                return EmployeeChangeStatus.Conflict;
            if (pendingActivation && user.IsInvitationSuspended == !active || !pendingActivation && user.IsActive == active)
                return EmployeeChangeStatus.Saved;

            var previous = user.IsActive;
            if (pendingActivation) user.SetInvitationSuspension(!active, actor.Id);
            else user.SetEmployeeAccess(active, actor.Id);
            var now = DateTimeOffset.UtcNow;
            await db.RefreshTokens.Where(x => x.UserId == id && x.RevokedAtUtc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAtUtc, now)
                    .SetProperty(x => x.RevocationReason, "AccountAccessChanged"), ct);
            db.AuditEntries.Add(new AuditEntry(actor.Id, id, "EmployeeAccessChanged", correlation,
                JsonSerializer.Serialize(new { previous, current = user.IsActive, user.IsInvitationSuspended, requestedActive = active })));
            db.Add(new OutboxMessage(new(Guid.NewGuid(), UserAccessChangedEvent.Changed, 1, "Identity", id,
                user.AuthorizationVersion, now, correlation, JsonSerializer.Serialize(new UserAccessChangedEvent(id, user.AuthorizationVersion)))));
            try
            {
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return EmployeeChangeStatus.Saved;
            }
            catch (DbUpdateConcurrencyException)
            {
                return EmployeeChangeStatus.Conflict;
            }
    }
    public async Task<EmployeeChangeStatus> ChangeProfileAsync(Guid id, EmployeeProfileInput input, string? departmentName, SessionUser actor, string correlation, CancellationToken ct)
    {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var user = await tickets.LockAsync(id, ct);
            if (user is null) return EmployeeChangeStatus.NotFound;
            var roles = await manager.GetRolesAsync(user);
            if (roles.Count != 1 || roles[0] is not ("Technician" or "DepartmentStaff")) return EmployeeChangeStatus.Forbidden;
            if (!input.RowVersion.SequenceEqual(user.RowVersion)) return EmployeeChangeStatus.Conflict;
            var before = new { user.FullName, user.DepartmentId, role = roles[0] };
            if (roles[0] != input.Role)
            {
                if (!(await manager.RemoveFromRoleAsync(user, roles[0])).Succeeded || !(await manager.AddToRoleAsync(user, input.Role!)).Succeeded) return EmployeeChangeStatus.Conflict;
            }
            user.ChangeEmployeeProfile(input.FullName!.Trim(), input.DepartmentId, departmentName, actor.Id);
            var now = DateTimeOffset.UtcNow;
            await db.RefreshTokens.Where(x => x.UserId == id && x.RevokedAtUtc == null).ExecuteUpdateAsync(s =>
                s.SetProperty(x => x.RevokedAtUtc, now).SetProperty(x => x.RevocationReason, "EmployeeProfileChanged"), ct);
            db.Add(new AuditEntry(actor.Id, id, "EmployeeProfileChanged", correlation,
                JsonSerializer.Serialize(new { before, after = new { input.FullName, input.Role, input.DepartmentId } })));
            db.Add(new OutboxMessage(new(Guid.NewGuid(), UserAccessChangedEvent.Changed, 1, "Identity", id,
                user.AuthorizationVersion, now, correlation, JsonSerializer.Serialize(new UserAccessChangedEvent(id, user.AuthorizationVersion)))));
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return EmployeeChangeStatus.Saved;
    }
}
