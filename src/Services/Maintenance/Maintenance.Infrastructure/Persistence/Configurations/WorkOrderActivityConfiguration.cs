using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderActivityConfiguration : IEntityTypeConfiguration<WorkOrderActivity>
{
    public void Configure(EntityTypeBuilder<WorkOrderActivity> builder)
    {
        builder.ToTable("WorkOrderActivities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.WorkOrderId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.Action).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired(true);
        builder.Property(x => x.FromStatus).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.ToStatus).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.FromTechnicianId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.ToTechnicianId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.ActorUserId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.Reason).HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.OccurredAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.HasIndex(x => new { x.WorkOrderId, x.OccurredAtUtc, x.Id }).HasDatabaseName("IX_WorkOrderActivities_WorkOrderId_OccurredAtUtc_Id");
        builder.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.NoAction);
    }
}
