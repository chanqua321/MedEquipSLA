using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Incident.Domain;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Incident.Infrastructure.Persistence.Configurations;

public sealed class SlaDeadlineAlertConfiguration : IEntityTypeConfiguration<SlaDeadlineAlert>
{
    public void Configure(EntityTypeBuilder<SlaDeadlineAlert> builder)
    {
        builder.ToTable("SlaDeadlineAlerts", table =>
        {
            table.HasCheckConstraint("CK_SlaDeadlineAlerts_AlertKind", "[AlertKind] IN (N'Response',N'Resolution')");
            table.HasCheckConstraint("CK_SlaDeadlineAlerts_AfterDeadline", "[DetectedAtUtc] > [DeadlineAtUtc]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.IncidentId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.AlertKind).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.DeadlineAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.DetectedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.EventId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.HasIndex(x => new { x.IncidentId, x.AlertKind }).HasDatabaseName("UX_SlaDeadlineAlerts_IncidentId_AlertKind").IsUnique();
        builder.HasIndex(x => x.EventId).HasDatabaseName("UX_SlaDeadlineAlerts_EventId").IsUnique();
        builder.HasOne<IncidentRecord>().WithMany().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.NoAction);
    }
}
