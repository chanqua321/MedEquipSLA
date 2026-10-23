using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Incident.Domain;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Incident.Infrastructure.Persistence.Configurations;

public sealed class ResolutionAttemptConfiguration : IEntityTypeConfiguration<ResolutionAttempt>
{
    public void Configure(EntityTypeBuilder<ResolutionAttempt> builder)
    {
        builder.ToTable("ResolutionAttempts", table =>
        {
            table.HasCheckConstraint("CK_ResolutionAttempts_AttemptNumber", "[AttemptNumber] > 0");
            table.HasCheckConstraint("CK_ResolutionAttempts_ReviewDecision", "[ReviewDecision] IN (N'Pending',N'Accepted',N'Rejected')");
            table.HasCheckConstraint("CK_ResolutionAttempts_Review", "([ReviewDecision] = N'Pending' AND [ReviewedByUserId] IS NULL AND [ReviewedAtUtc] IS NULL) OR ([ReviewDecision] IN (N'Accepted',N'Rejected') AND [ReviewedByUserId] IS NOT NULL AND [ReviewedAtUtc] IS NOT NULL AND [ReviewedAtUtc] >= [CompletedAtUtc])");
            table.HasCheckConstraint("CK_ResolutionAttempts_RejectedReason", "[ReviewDecision] <> N'Rejected' OR NULLIF(LTRIM(RTRIM([ReviewReason])),N'') IS NOT NULL");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.IncidentId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.AttemptNumber).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.PerformedByUserId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.ResolutionNotes).HasColumnType("nvarchar(4000)").HasMaxLength(4000).IsRequired(true);
        builder.Property(x => x.ResultSummary).HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired(true);
        builder.Property(x => x.CompletedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.ReviewDecision).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.ReviewedByUserId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.ReviewedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.ReviewReason).HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        builder.HasIndex(x => new { x.IncidentId, x.AttemptNumber }).HasDatabaseName("UX_ResolutionAttempts_IncidentId_AttemptNumber").IsUnique();
        builder.HasIndex(x => x.IncidentId).HasDatabaseName("UX_ResolutionAttempts_IncidentId").IsUnique().HasFilter("[ReviewDecision] = N'Accepted'");
        builder.HasOne<IncidentRecord>().WithMany().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.NoAction);
    }
}
