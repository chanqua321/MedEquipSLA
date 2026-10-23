using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Incident.Domain;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Incident.Infrastructure.Persistence.Configurations;

public sealed class IncidentActivityConfiguration : IEntityTypeConfiguration<IncidentActivity>
{
    public void Configure(EntityTypeBuilder<IncidentActivity> builder)
    {
        builder.ToTable("IncidentActivities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.IncidentId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.Action).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired(true);
        builder.Property(x => x.FromStatus).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.ToStatus).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.FromTechnicianId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.ToTechnicianId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.ActorUserId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.Reason).HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.OccurredAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.HasIndex(x => new { x.IncidentId, x.OccurredAtUtc, x.Id }).HasDatabaseName("IX_IncidentActivities_IncidentId_OccurredAtUtc_Id");
        builder.HasOne<IncidentRecord>().WithMany().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.NoAction);
    }
}
