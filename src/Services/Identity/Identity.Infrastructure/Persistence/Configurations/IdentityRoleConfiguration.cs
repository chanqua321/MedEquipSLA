using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Identity.Domain;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using MedEquipSLA.Identity.Infrastructure.Persistence;

namespace MedEquipSLA.Identity.Infrastructure.Persistence.Configurations;

public sealed class IdentityRoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        builder.ToTable("AspNetRoles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.Name).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.NormalizedName).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.ConcurrencyStamp).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.HasIndex(x => x.NormalizedName).HasDatabaseName("UX_AspNetRoles_NormalizedName").IsUnique().HasFilter("[NormalizedName] IS NOT NULL");
    }
}
