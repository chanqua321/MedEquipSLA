using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Identity.Domain;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using MedEquipSLA.Identity.Infrastructure.Persistence;

namespace MedEquipSLA.Identity.Infrastructure.Persistence.Configurations;

public sealed class IdentityUserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder)
    {
        builder.ToTable("AspNetUserRoles");
        builder.HasKey(x => new { x.UserId, x.RoleId });
        builder.Property(x => x.UserId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.RoleId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<IdentityRole<Guid>>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.NoAction);
    }
}
