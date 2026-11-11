using MedEquipSLA.Identity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Identity.Infrastructure.Persistence;

public sealed class IdentityRecipientDirectory(IdentityDbContext db) : IIdentityRecipientDirectory
{
    public async Task<Guid[]> RecipientsAsync(Guid? technician, CancellationToken ct) => await (
        from user in db.Users.AsNoTracking() join membership in db.UserRoles on user.Id equals membership.UserId
        join role in db.Roles on membership.RoleId equals role.Id
        where user.IsActive && user.PasswordHash != null &&
            (role.Name == "Admin" || role.Name == "Technician" && user.Id == technician && user.EmailConfirmed)
        select user.Id).Distinct().ToArrayAsync(ct);
    public async Task<ActiveRecipientEmail?> EmailAsync(Guid id, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(x => x.Id == id && x.IsActive && x.PasswordHash != null && x.EmailConfirmed && x.Email != null)
            .Select(x => new ActiveRecipientEmail(x.Id, x.Email!)).SingleOrDefaultAsync(ct);
    public async Task<TechnicianAccess> TechnicianAsync(Guid id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        var eligible = user is { IsActive: true, EmailConfirmed: true, PasswordHash: not null } && await (
            from membership in db.UserRoles join role in db.Roles on membership.RoleId equals role.Id
            where membership.UserId == id && role.Name == "Technician" select membership.UserId).AnyAsync(ct);
        return new(id, eligible, user?.AuthorizationVersion ?? 0);
    }
}
