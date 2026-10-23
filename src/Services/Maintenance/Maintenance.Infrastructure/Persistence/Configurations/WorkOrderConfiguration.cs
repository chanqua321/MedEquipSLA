using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("WorkOrders", table =>
        {
            table.HasCheckConstraint("CK_WorkOrders_AggregateVersion", "[AggregateVersion] >= 1");
            table.HasCheckConstraint("CK_WorkOrders_WorkType", "[WorkType] IN (N'Maintenance',N'Calibration')");
            table.HasCheckConstraint("CK_WorkOrders_Status", "[Status] IN (N'Scheduled',N'InProgress',N'Completed',N'Closed',N'Cancelled')");
            table.HasCheckConstraint("CK_WorkOrders_Deadline", "[DueAtUtc] >= [ScheduledForUtc]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.Code).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired(true);
        builder.Property(x => x.OccurrenceId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.EquipmentId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.EquipmentCodeSnapshot).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired(true);
        builder.Property(x => x.EquipmentNameSnapshot).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(true);
        builder.Property(x => x.DepartmentIdSnapshot).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.DepartmentNameSnapshot).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(true);
        builder.Property(x => x.WorkType).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.Status).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.AssignedTechnicianId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.ScheduledForUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.DueAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.StartedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.ClosedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.CancelledAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.CancellationReason).HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.IsBlocking).HasColumnType("bit").IsRequired(true);
        builder.Property(x => x.EquipmentUseRegistrationId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.AggregateVersion).HasColumnType("bigint").IsRequired(true);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.CreatedByUserId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.UpdatedByUserId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        builder.HasIndex(x => x.Code).HasDatabaseName("UX_WorkOrders_Code").IsUnique();
        builder.HasIndex(x => x.OccurrenceId).HasDatabaseName("UX_WorkOrders_OccurrenceId").IsUnique().HasFilter("[OccurrenceId] IS NOT NULL");
        builder.HasIndex(x => new { x.Status, x.DueAtUtc, x.Id }).HasDatabaseName("IX_WorkOrders_Status_DueAtUtc_Id");
        builder.HasIndex(x => new { x.AssignedTechnicianId, x.Status }).HasDatabaseName("IX_WorkOrders_AssignedTechnicianId_Status");
        builder.HasIndex(x => new { x.EquipmentId, x.Status }).HasDatabaseName("IX_WorkOrders_EquipmentId_Status");
        builder.HasOne<ScheduleOccurrence>().WithOne().HasForeignKey<WorkOrder>(x => x.OccurrenceId).OnDelete(DeleteBehavior.NoAction);
    }
}
