using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Identity.Domain;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using MedEquipSLA.Identity.Infrastructure.Persistence;

namespace MedEquipSLA.Identity.Infrastructure.Persistence.Configurations;

public sealed class IdentityUserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("AspNetUserClaims");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.UserId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.ClaimType).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.ClaimValue).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
