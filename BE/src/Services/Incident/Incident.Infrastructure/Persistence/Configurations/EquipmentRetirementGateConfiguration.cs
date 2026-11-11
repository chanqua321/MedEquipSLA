using MedEquipSLA.Application.Coordination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedEquipSLA.Incident.Infrastructure.Persistence.Configurations;

public sealed class EquipmentRetirementGateConfiguration : IEntityTypeConfiguration<EquipmentRetirementGate>
{
    public void Configure(EntityTypeBuilder<EquipmentRetirementGate> b)
    {
        b.ToTable("EquipmentRetirementGates", t =>
        {
            t.HasCheckConstraint("CK_SourceRetirementGate_Phase", "[Phase] IN (N'Open',N'Frozen',N'Sealed',N'Retired')");
            t.HasCheckConstraint("CK_SourceRetirementGate_Version", "[HighestRetirementEpoch]>=0 AND [SourceGateVersion]>=0");
            t.HasCheckConstraint("CK_SourceRetirementGate_Binding", "([HighestRetirementEpoch]=0 AND [CurrentIntentId] IS NULL AND [Phase]=N'Open' AND [ReadyProofId] IS NULL AND [CancelAcknowledgementId] IS NULL) OR ([HighestRetirementEpoch]>0 AND [CurrentIntentId] IS NOT NULL)");
            t.HasCheckConstraint("CK_SourceRetirementGate_Proof", "([Phase]=N'Frozen' AND [ReadyProofId] IS NULL AND [CancelAcknowledgementId] IS NULL) OR ([Phase] IN (N'Sealed',N'Retired') AND [ReadyProofId] IS NOT NULL AND [CancelAcknowledgementId] IS NULL) OR ([Phase]=N'Open' AND ([HighestRetirementEpoch]=0 OR [CancelAcknowledgementId] IS NOT NULL))");
            t.HasCheckConstraint("CK_SourceRetirementGate_Utc", "DATEPART(TZOFFSET,[CreatedAtUtc])=0 AND DATEPART(TZOFFSET,[UpdatedAtUtc])=0 AND [UpdatedAtUtc]>=[CreatedAtUtc]");
        });
        b.HasKey(x => x.EquipmentId);
        b.Property(x => x.EquipmentId).HasColumnType("uniqueidentifier");
        b.Property(x => x.CurrentIntentId).HasColumnType("uniqueidentifier");
        b.Property(x => x.HighestRetirementEpoch).HasColumnType("bigint");
        b.Property(x => x.Phase).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired();
        b.Property(x => x.SourceGateVersion).HasColumnType("bigint");
        b.Property(x => x.ReadyProofId).HasColumnType("uniqueidentifier");
        b.Property(x => x.CancelAcknowledgementId).HasColumnType("uniqueidentifier");
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset");
        b.Property(x => x.UpdatedAtUtc).HasColumnType("datetimeoffset");
        b.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        b.HasIndex(x => new { x.Phase, x.UpdatedAtUtc, x.EquipmentId });
    }
}
