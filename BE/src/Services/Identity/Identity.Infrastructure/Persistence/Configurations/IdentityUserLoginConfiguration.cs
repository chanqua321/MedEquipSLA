using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Identity.Domain;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using MedEquipSLA.Identity.Infrastructure.Persistence;

namespace MedEquipSLA.Identity.Infrastructure.Persistence.Configurations;

public sealed class IdentityUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("AspNetUserLogins");
        builder.HasKey(x => new { x.LoginProvider, x.ProviderKey });
        builder.Property(x => x.LoginProvider).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired(true);
        builder.Property(x => x.ProviderKey).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired(true);
        builder.Property(x => x.ProviderDisplayName).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.UserId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
