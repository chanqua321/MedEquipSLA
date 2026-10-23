using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence.Configurations;

public sealed class OccurrenceReminderConfiguration : IEntityTypeConfiguration<OccurrenceReminder>
{
    public void Configure(EntityTypeBuilder<OccurrenceReminder> builder)
    {
        builder.ToTable("OccurrenceReminders", table =>
        {
            table.HasCheckConstraint("CK_OccurrenceReminders_ReminderKind", "[ReminderKind] IN (N'DueSoon',N'Overdue')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.OccurrenceId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.ReminderKind).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.EventId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.TriggeredAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.HasIndex(x => new { x.OccurrenceId, x.ReminderKind }).HasDatabaseName("UX_OccurrenceReminders_OccurrenceId_ReminderKind").IsUnique();
        builder.HasIndex(x => x.EventId).HasDatabaseName("UX_OccurrenceReminders_EventId").IsUnique();
        builder.HasOne<ScheduleOccurrence>().WithMany().HasForeignKey(x => x.OccurrenceId).OnDelete(DeleteBehavior.NoAction);
    }
}
