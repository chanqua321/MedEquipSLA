using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence.Configurations;

public sealed class EquipmentStateProjectionConfiguration : IEntityTypeConfiguration<EquipmentStateProjection>
{
    public void Configure(EntityTypeBuilder<EquipmentStateProjection> builder)
    {
        builder.ToTable("EquipmentStateProjections", table =>
        {
            table.HasCheckConstraint("CK_EquipmentStateProjections_SourceService", "[SourceService] IN (N'Incident',N'Maintenance')");
            table.HasCheckConstraint("CK_EquipmentStateProjections_SourceVersion", "[SourceVersion] >= 1");
        });
        builder.HasKey(x => new { x.SourceService, x.SourceAggregateId });
        builder.Property(x => x.SourceService).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired(true);
        builder.Property(x => x.SourceAggregateId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.EquipmentId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.SourceStatus).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired(true);
        builder.Property(x => x.IsBlocking).HasColumnType("bit").IsRequired(true);
        builder.Property(x => x.SourceVersion).HasColumnType("bigint").IsRequired(true);
        builder.Property(x => x.LastEventId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.LastOccurredAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.AppliedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        builder.HasIndex(x => new { x.EquipmentId, x.IsBlocking }).HasDatabaseName("IX_EquipmentStateProjections_EquipmentId_IsBlocking");
        builder.HasOne<EquipmentAsset>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.NoAction);
    }
}
