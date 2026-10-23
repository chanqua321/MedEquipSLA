using Microsoft.EntityFrameworkCore;
using MedEquipSLA.Application;
using MedEquipSLA.Identity.Domain;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace MedEquipSLA.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IUnitOfWork
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.Entity<IdentityRole<Guid>>().HasData(
            new IdentityRole<Guid> { Id = Guid.Parse("d622a4c1-e926-4f60-a357-000000000001"), Name = "Admin", NormalizedName = "ADMIN", ConcurrencyStamp = "medequip-admin-v1" },
            new IdentityRole<Guid> { Id = Guid.Parse("d622a4c1-e926-4f60-a357-000000000002"), Name = "Technician", NormalizedName = "TECHNICIAN", ConcurrencyStamp = "medequip-technician-v1" },
            new IdentityRole<Guid> { Id = Guid.Parse("d622a4c1-e926-4f60-a357-000000000003"), Name = "DepartmentStaff", NormalizedName = "DEPARTMENTSTAFF", ConcurrencyStamp = "medequip-staff-v1" });
    }
}
