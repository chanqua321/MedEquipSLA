using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Incident.Domain;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Incident.Infrastructure.Persistence.Configurations;

public sealed class IncidentRecordConfiguration : IEntityTypeConfiguration<IncidentRecord>
{
    public void Configure(EntityTypeBuilder<IncidentRecord> builder)
    {
        builder.ToTable("Incidents", table =>
        {
            table.HasCheckConstraint("CK_Incidents_AggregateVersion", "[AggregateVersion] >= 1");
            table.HasCheckConstraint("CK_Incidents_Priority", "[Priority] IN (N'Critical',N'High',N'Medium',N'Low')");
            table.HasCheckConstraint("CK_Incidents_SlaPrioritySnapshot", "[SlaPrioritySnapshot] IN (N'Critical',N'High',N'Medium',N'Low')");
            table.HasCheckConstraint("CK_Incidents_Status", "[Status] IN (N'Open',N'InProgress',N'Resolved',N'Closed')");
            table.HasCheckConstraint("CK_Incidents_ResponseOutcome", "[ResponseOutcome] IN (N'Pending',N'Met',N'Breached')");
            table.HasCheckConstraint("CK_Incidents_ResolutionOutcome", "[ResolutionOutcome] IN (N'Pending',N'Met',N'Breached')");
            table.HasCheckConstraint("CK_Incidents_RelatedIncident", "[RelatedIncidentId] IS NULL OR [RelatedIncidentId] <> [Id]");
            table.HasCheckConstraint("CK_Incidents_SlaDurations", "[ResponseDurationMinutesSnapshot] > 0 AND [ResolutionDurationMinutesSnapshot] >= [ResponseDurationMinutesSnapshot]");
            table.HasCheckConstraint("CK_Incidents_SlaDeadlines", "[ResponseDeadlineAtUtc] = DATEADD(minute,[ResponseDurationMinutesSnapshot],[CreatedAtUtc]) AND [ResolutionDeadlineAtUtc] = DATEADD(minute,[ResolutionDurationMinutesSnapshot],[CreatedAtUtc])");
            table.HasCheckConstraint("CK_Incidents_Finalization", "([Status] = N'Closed' AND [ClosedAtUtc] IS NOT NULL AND [SlaFinalizedAtUtc] IS NOT NULL AND [ResponseOutcome] <> N'Pending' AND [ResolutionOutcome] <> N'Pending') OR ([Status] <> N'Closed' AND [ClosedAtUtc] IS NULL AND [SlaFinalizedAtUtc] IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.Code).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired(true);
        builder.Property(x => x.EquipmentId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.EquipmentCodeSnapshot).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired(true);
        builder.Property(x => x.EquipmentNameSnapshot).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(true);
        builder.Property(x => x.DepartmentIdSnapshot).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.DepartmentNameSnapshot).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(true);
        builder.Property(x => x.Title).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(true);
        builder.Property(x => x.Description).HasColumnType("nvarchar(4000)").HasMaxLength(4000).IsRequired(true);
        builder.Property(x => x.ReportedByUserId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.AssignedTechnicianId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.RelatedIncidentId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.Priority).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.Status).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.SlaPolicyId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.SlaPolicyVersion).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.SlaPrioritySnapshot).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.ResponseDurationMinutesSnapshot).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.ResolutionDurationMinutesSnapshot).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.ResponseDeadlineAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.ResolutionDeadlineAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.FirstRespondedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.ClosedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.ResponseOutcome).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.ResolutionOutcome).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.SlaFinalizedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.EquipmentUseRegistrationId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.AggregateVersion).HasColumnType("bigint").IsRequired(true);
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.CreatedByUserId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.UpdatedByUserId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        builder.HasIndex(x => x.Code).HasDatabaseName("UX_Incidents_Code").IsUnique();
        builder.HasIndex(x => new { x.DepartmentIdSnapshot, x.CreatedAtUtc, x.Id }).HasDatabaseName("IX_Incidents_DepartmentIdSnapshot_CreatedAtUtc_Id");
        builder.HasIndex(x => new { x.Status, x.ResponseDeadlineAtUtc }).HasDatabaseName("IX_Incidents_Status_ResponseDeadlineAtUtc");
        builder.HasIndex(x => new { x.Status, x.ResolutionDeadlineAtUtc }).HasDatabaseName("IX_Incidents_Status_ResolutionDeadlineAtUtc");
        builder.HasIndex(x => new { x.AssignedTechnicianId, x.Status }).HasDatabaseName("IX_Incidents_AssignedTechnicianId_Status");
        builder.HasIndex(x => new { x.EquipmentId, x.Status }).HasDatabaseName("IX_Incidents_EquipmentId_Status");
        builder.HasOne<SlaPolicyVersion>().WithMany().HasForeignKey(x => new { x.SlaPolicyId, x.SlaPolicyVersion }).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<IncidentRecord>().WithMany().HasForeignKey(x => x.RelatedIncidentId).OnDelete(DeleteBehavior.NoAction);
    }
}
