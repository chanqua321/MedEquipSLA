using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence.Configurations;

public sealed class ReconciliationCheckpointConfiguration : IEntityTypeConfiguration<ReconciliationCheckpoint>
{
    public void Configure(EntityTypeBuilder<ReconciliationCheckpoint> builder)
    {
        builder.ToTable("ReconciliationCheckpoints", table =>
        {
            table.HasCheckConstraint("CK_ReconciliationCheckpoints_SourceService", "[SourceService] IN (N'Incident',N'Maintenance')");
            table.HasCheckConstraint("CK_ReconciliationCheckpoints_State", "[State] IN (N'Idle',N'Running',N'Completed',N'Failed')");
        });
        builder.HasKey(x => x.SourceService);
        builder.Property(x => x.SourceService).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired(true);
        builder.Property(x => x.SnapshotId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.SnapshotWatermark).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.Cursor).HasColumnType("nvarchar(512)").HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.State).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.StartedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.LastCompletedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.LastError).HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
    }
}
