using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderAttemptConfiguration : IEntityTypeConfiguration<WorkOrderAttempt>
{
    public void Configure(EntityTypeBuilder<WorkOrderAttempt> builder)
    {
        builder.ToTable("WorkOrderAttempts", table =>
        {
            table.HasCheckConstraint("CK_WorkOrderAttempts_AttemptNumber", "[AttemptNumber] > 0");
            table.HasCheckConstraint("CK_WorkOrderAttempts_ReviewDecision", "[ReviewDecision] IN (N'Pending',N'Accepted',N'Rejected')");
            table.HasCheckConstraint("CK_WorkOrderAttempts_Review", "([ReviewDecision] = N'Pending' AND [ReviewedByUserId] IS NULL AND [ReviewedAtUtc] IS NULL) OR ([ReviewDecision] IN (N'Accepted',N'Rejected') AND [ReviewedByUserId] IS NOT NULL AND [ReviewedAtUtc] IS NOT NULL AND [ReviewedAtUtc] >= [CompletedAtUtc])");
            table.HasCheckConstraint("CK_WorkOrderAttempts_RejectedReason", "[ReviewDecision] <> N'Rejected' OR NULLIF(LTRIM(RTRIM([ReviewReason])),N'') IS NOT NULL");
            table.HasCheckConstraint("CK_WorkOrderAttempts_ResultStatus", "[ResultStatus] IN (N'Passed',N'Failed')");
            table.HasCheckConstraint("CK_WorkOrderAttempts_Cost", "[Cost] IS NULL OR [Cost] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.WorkOrderId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.AttemptNumber).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.PerformedByUserId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.CompletedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.ResultStatus).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.Notes).HasColumnType("nvarchar(4000)").HasMaxLength(4000).IsRequired(true);
        builder.Property(x => x.Cost).HasColumnType("decimal(18,2)").HasPrecision(18, 2).IsRequired(false);
        builder.Property(x => x.CurrencyCode).HasColumnType("char(3)").HasMaxLength(3).IsRequired(false);
        builder.Property(x => x.ReviewDecision).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.ReviewedByUserId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.ReviewedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.ReviewReason).HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        builder.HasIndex(x => new { x.WorkOrderId, x.AttemptNumber }).HasDatabaseName("UX_WorkOrderAttempts_WorkOrderId_AttemptNumber").IsUnique();
        builder.HasIndex(x => x.WorkOrderId).HasDatabaseName("UX_WorkOrderAttempts_WorkOrderId").IsUnique().HasFilter("[ReviewDecision] = N'Accepted'");
        builder.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.NoAction);
    }
}
