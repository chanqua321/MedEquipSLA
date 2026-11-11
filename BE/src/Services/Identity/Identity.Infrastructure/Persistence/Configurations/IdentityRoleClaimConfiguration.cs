using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Identity.Domain;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using MedEquipSLA.Identity.Infrastructure.Persistence;

namespace MedEquipSLA.Identity.Infrastructure.Persistence.Configurations;

public sealed class IdentityRoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder)
    {
        builder.ToTable("AspNetRoleClaims");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.RoleId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.ClaimType).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.ClaimValue).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.HasOne<IdentityRole<Guid>>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.NoAction);
    }
}
