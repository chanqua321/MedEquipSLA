using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Identity.Domain;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using MedEquipSLA.Identity.Infrastructure.Persistence;

namespace MedEquipSLA.Identity.Infrastructure.Persistence.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries", table =>
        {
            table.HasCheckConstraint("CK_AuditEntries_Json", "[ChangesJson] IS NULL OR ISJSON([ChangesJson]) = 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.ActorUserId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.ActorService).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.Action).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired(true);
        builder.Property(x => x.EntityType).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired(true);
        builder.Property(x => x.EntityId).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired(true);
        builder.Property(x => x.OccurredAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.CorrelationId).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired(true);
        builder.Property(x => x.ChangesJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAtUtc }).HasDatabaseName("IX_AuditEntries_EntityType_EntityId_OccurredAtUtc");
    }
}
