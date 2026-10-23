using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Identity.Domain;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using MedEquipSLA.Identity.Infrastructure.Persistence;

namespace MedEquipSLA.Identity.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("AspNetUsers", table =>
        {
            table.HasCheckConstraint("CK_AspNetUsers_AuthorizationVersion", "[AuthorizationVersion] >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.UserName).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.NormalizedUserName).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.Email).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.NormalizedEmail).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.EmailConfirmed).HasColumnType("bit").IsRequired(true);
        builder.Property(x => x.PasswordHash).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.SecurityStamp).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.ConcurrencyStamp).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.PhoneNumber).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.PhoneNumberConfirmed).HasColumnType("bit").IsRequired(true);
        builder.Property(x => x.TwoFactorEnabled).HasColumnType("bit").IsRequired(true);
        builder.Property(x => x.LockoutEnd).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.LockoutEnabled).HasColumnType("bit").IsRequired(true);
        builder.Property(x => x.AccessFailedCount).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.FullName).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(true);
        builder.Property(x => x.DepartmentId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.DepartmentNameSnapshot).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.IsActive).HasColumnType("bit").IsRequired(true);
        builder.Property(x => x.AuthorizationVersion).HasColumnType("bigint").IsRequired(true);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.CreatedByUserId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.UpdatedByUserId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        builder.HasIndex(x => x.NormalizedUserName).HasDatabaseName("UX_AspNetUsers_NormalizedUserName").IsUnique().HasFilter("[NormalizedUserName] IS NOT NULL");
        builder.HasIndex(x => x.NormalizedEmail).HasDatabaseName("UX_AspNetUsers_NormalizedEmail").IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
    }
}
