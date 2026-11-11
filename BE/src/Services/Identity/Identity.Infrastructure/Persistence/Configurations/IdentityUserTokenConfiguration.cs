using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Identity.Domain;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using MedEquipSLA.Identity.Infrastructure.Persistence;

namespace MedEquipSLA.Identity.Infrastructure.Persistence.Configurations;

public sealed class IdentityUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("AspNetUserTokens");
        builder.HasKey(x => new { x.UserId, x.LoginProvider, x.Name });
        builder.Property(x => x.UserId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.LoginProvider).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired(true);
        builder.Property(x => x.Name).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired(true);
        builder.Property(x => x.Value).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
