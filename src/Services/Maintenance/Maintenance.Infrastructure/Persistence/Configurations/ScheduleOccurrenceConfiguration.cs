using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence.Configurations;

public sealed class ScheduleOccurrenceConfiguration : IEntityTypeConfiguration<ScheduleOccurrence>
{
    public void Configure(EntityTypeBuilder<ScheduleOccurrence> builder)
    {
        builder.ToTable("ScheduleOccurrences", table =>
        {
            table.HasCheckConstraint("CK_ScheduleOccurrences_State", "[State] IN (N'Planned',N'WorkOrderCreated',N'Cancelled')");
            table.HasCheckConstraint("CK_ScheduleOccurrences_Deadline", "[DueAtUtc] >= [ScheduledOccurrenceAtUtc]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.ScheduleId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.ScheduledOccurrenceAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.DueAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.State).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.CancelledAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        builder.HasIndex(x => new { x.ScheduleId, x.ScheduledOccurrenceAtUtc }).HasDatabaseName("UX_ScheduleOccurrences_ScheduleId_ScheduledOccurrenceAtUtc").IsUnique();
        builder.HasIndex(x => new { x.State, x.ScheduledOccurrenceAtUtc }).HasDatabaseName("IX_ScheduleOccurrences_State_ScheduledOccurrenceAtUtc");
        builder.HasOne<MaintenanceSchedule>().WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.NoAction);
    }
}
