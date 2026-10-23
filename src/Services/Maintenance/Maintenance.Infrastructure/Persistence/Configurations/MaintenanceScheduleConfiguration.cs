using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence.Configurations;

public sealed class MaintenanceScheduleConfiguration : IEntityTypeConfiguration<MaintenanceSchedule>
{
    public void Configure(EntityTypeBuilder<MaintenanceSchedule> builder)
    {
        builder.ToTable("MaintenanceSchedules", table =>
        {
            table.HasCheckConstraint("CK_MaintenanceSchedules_AggregateVersion", "[AggregateVersion] >= 1");
            table.HasCheckConstraint("CK_MaintenanceSchedules_WorkType", "[WorkType] IN (N'Maintenance',N'Calibration')");
            table.HasCheckConstraint("CK_MaintenanceSchedules_Intervals", "[IntervalDays] > 0 AND [ReminderLeadMinutes] >= 0 AND [CompletionWindowMinutes] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.Code).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired(true);
        builder.Property(x => x.EquipmentId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.EquipmentCodeSnapshot).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired(true);
        builder.Property(x => x.EquipmentNameSnapshot).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(true);
        builder.Property(x => x.DepartmentIdSnapshot).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.DepartmentNameSnapshot).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(true);
        builder.Property(x => x.WorkType).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.AnchorAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.IntervalDays).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.NextOccurrenceAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.ReminderLeadMinutes).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.CompletionWindowMinutes).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.IsActive).HasColumnType("bit").IsRequired(true);
        builder.Property(x => x.EquipmentUseRegistrationId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.AggregateVersion).HasColumnType("bigint").IsRequired(true);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.CreatedByUserId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.UpdatedByUserId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        builder.HasIndex(x => x.Code).HasDatabaseName("UX_MaintenanceSchedules_Code").IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.NextOccurrenceAtUtc }).HasDatabaseName("IX_MaintenanceSchedules_IsActive_NextOccurrenceAtUtc");
    }
}
