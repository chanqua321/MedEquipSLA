using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence.Configurations;

public sealed class EquipmentStatusHistoryConfiguration : IEntityTypeConfiguration<EquipmentStatusHistory>
{
    public void Configure(EntityTypeBuilder<EquipmentStatusHistory> builder)
    {
        builder.ToTable("EquipmentStatusHistories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.EquipmentId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.ChangeKind).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired(true);
        builder.Property(x => x.FromStatus).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired(false);
        builder.Property(x => x.ToStatus).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired(false);
        builder.Property(x => x.FromDepartmentId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.ToDepartmentId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.FromDepartmentNameSnapshot).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.ToDepartmentNameSnapshot).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.Reason).HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.ActorUserId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.SourceEventId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.ChangedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.HasIndex(x => new { x.EquipmentId, x.ChangedAtUtc, x.Id }).HasDatabaseName("IX_EquipmentStatusHistories_EquipmentId_ChangedAtUtc_Id");
        builder.HasIndex(x => x.SourceEventId).HasDatabaseName("UX_EquipmentStatusHistories_SourceEventId").IsUnique().HasFilter("[SourceEventId] IS NOT NULL");
        builder.HasOne<EquipmentAsset>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.FromDepartmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.ToDepartmentId).OnDelete(DeleteBehavior.NoAction);
    }
}
