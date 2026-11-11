using System.Data;
using MedEquipSLA.Identity.Application.Abstractions;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Identity.Infrastructure.Persistence;

// Deployment-only port: it is never mapped as an HTTP signup endpoint.
public sealed class AdminProvisioning(IdentityDbContext db, UserManager<ApplicationUser> users) : IAdminProvisioning
{
    public async Task ProvisionAsync(string userName, string email, string password, CancellationToken ct)
    {
        userName = userName.Trim(); email = email.Trim();
        if (userName.Length is < 1 or > 100 || email.Length is < 1 or > 256 || string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Configure deployment Admin credentials before provisioning.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'MedEquip.Identity.SingleAdmin',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51077,'Admin provisioning is busy.',1", ct);
        if (await users.FindByNameAsync(userName) is not null || (await users.GetUsersInRoleAsync("Admin")).Count != 0)
            throw new InvalidOperationException("An administrator or conflicting username already exists.");
        var user = ApplicationUser.CreateAdmin(userName, email);
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded) throw new InvalidOperationException("Administrator creation failed: " + string.Join(",", created.Errors.Select(x => x.Code)));
        if (!(await users.AddToRoleAsync(user, "Admin")).Succeeded) throw new InvalidOperationException("Administrator role assignment failed.");
        db.Add(AuditEntry.ForSecurityService(user.Id, "AdminProvisioned", "{}"));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
