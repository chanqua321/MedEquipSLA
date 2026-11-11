using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Incident.Domain;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Incident.Infrastructure.Persistence.Configurations;

public sealed class SlaPolicyVersionConfiguration : IEntityTypeConfiguration<SlaPolicyVersion>
{
    public void Configure(EntityTypeBuilder<SlaPolicyVersion> builder)
    {
        builder.ToTable("SlaPolicyVersions", table =>
        {
            table.HasCheckConstraint("CK_SlaPolicyVersions_Durations", "[Version] > 0 AND [ResponseDurationMinutes] > 0 AND [ResolutionDurationMinutes] >= [ResponseDurationMinutes]");
            table.HasCheckConstraint("CK_SlaPolicyVersions_EffectiveInterval", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
        });
        builder.HasKey(x => new { x.SlaPolicyId, x.Version });
        builder.Property(x => x.SlaPolicyId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.Version).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.ResponseDurationMinutes).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.ResolutionDurationMinutes).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.EffectiveFromUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.EffectiveToUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.CreatedByUserId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.HasIndex(x => new { x.SlaPolicyId, x.EffectiveFromUtc }).HasDatabaseName("UX_SlaPolicyVersions_SlaPolicyId_EffectiveFromUtc").IsUnique();
        builder.HasOne<SlaPolicy>().WithMany().HasForeignKey(x => x.SlaPolicyId).OnDelete(DeleteBehavior.NoAction);
    }
}
