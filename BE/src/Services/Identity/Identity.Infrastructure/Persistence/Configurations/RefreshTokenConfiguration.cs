using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Identity.Domain;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using MedEquipSLA.Identity.Infrastructure.Persistence;

namespace MedEquipSLA.Identity.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens", table =>
        {
            table.HasCheckConstraint("CK_RefreshTokens_Expiration", "[ExpiresAtUtc] > [IssuedAtUtc]");
            table.HasCheckConstraint("CK_RefreshTokens_Replacement", "[ReplacedByTokenId] IS NULL OR [ReplacedByTokenId] <> [Id]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.UserId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.TokenHash).HasColumnType("binary(32)").HasMaxLength(32).IsRequired(true);
        builder.Property(x => x.FamilyId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.IssuedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.ExpiresAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.RevokedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.ReplacedByTokenId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.RevocationReason).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(false);
        builder.HasIndex(x => x.TokenHash).HasDatabaseName("UX_RefreshTokens_TokenHash").IsUnique();
        builder.HasIndex(x => x.ReplacedByTokenId).HasDatabaseName("UX_RefreshTokens_ReplacedByTokenId").IsUnique().HasFilter("[ReplacedByTokenId] IS NOT NULL");
        builder.HasIndex(x => new { x.UserId, x.FamilyId }).HasDatabaseName("IX_RefreshTokens_UserId_FamilyId");
        builder.HasIndex(x => new { x.UserId, x.ExpiresAtUtc }).HasDatabaseName("IX_RefreshTokens_UserId_ExpiresAtUtc");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<RefreshToken>().WithOne().HasForeignKey<RefreshToken>(x => x.ReplacedByTokenId).OnDelete(DeleteBehavior.NoAction);
    }
}
