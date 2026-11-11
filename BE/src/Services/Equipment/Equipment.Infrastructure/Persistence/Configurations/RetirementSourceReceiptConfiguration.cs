using MedEquipSLA.Equipment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence.Configurations;

public sealed class RetirementSourceReceiptConfiguration : IEntityTypeConfiguration<RetirementSourceReceipt>
{
    public void Configure(EntityTypeBuilder<RetirementSourceReceipt> b)
    {
        b.ToTable("RetirementSourceReceipts", t =>
        {
            t.HasCheckConstraint("CK_RetirementReceipts_Source", "[SourceService] IN (N'Maintenance',N'Incident')");
            t.HasCheckConstraint("CK_RetirementReceipts_Version", "[CoverageVersion]=1 AND [RetirementEpoch]>0 AND [SourceGateVersion]>0");
            t.HasCheckConstraint("CK_RetirementReceipts_State", "[SourceState] IN (N'Frozen',N'Blocked',N'Sealed',N'CancelAck')");
            t.HasCheckConstraint("CK_RetirementReceipts_Proof", "([SourceState] IN (N'Frozen',N'Blocked') AND [ReadyProofId] IS NULL AND [CancelAcknowledgementId] IS NULL) OR ([SourceState]=N'Sealed' AND [ReadyProofId] IS NOT NULL AND [CancelAcknowledgementId] IS NULL) OR ([SourceState]=N'CancelAck' AND [CancelAcknowledgementId] IS NOT NULL)");
            t.HasCheckConstraint("CK_RetirementReceipts_Utc", "DATEPART(TZOFFSET,[ReceivedAtUtc])=0");
        });
        b.HasKey(x => new { x.IntentId, x.SourceService });
        b.Property(x => x.IntentId).HasColumnType("uniqueidentifier");
        b.Property(x => x.EquipmentId).HasColumnType("uniqueidentifier");
        b.Property(x => x.RetirementEpoch).HasColumnType("bigint");
        b.Property(x => x.SourceService).HasColumnType("nvarchar(30)").HasMaxLength(30);
        b.Property(x => x.CoverageVersion).HasColumnType("int");
        b.Property(x => x.SourceGateVersion).HasColumnType("bigint");
        b.Property(x => x.ReadyProofId).HasColumnType("uniqueidentifier");
        b.Property(x => x.CancelAcknowledgementId).HasColumnType("uniqueidentifier");
        b.Property(x => x.SourceState).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired();
        b.Property(x => x.ReceivedAtUtc).HasColumnType("datetimeoffset");
        b.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        b.HasIndex(x => new { x.EquipmentId, x.RetirementEpoch, x.SourceService });
        b.HasOne<EquipmentRetirementIntent>().WithMany(x => x.Receipts)
            .HasForeignKey(x => new { x.IntentId, x.EquipmentId, x.RetirementEpoch })
            .HasPrincipalKey(x => new { x.Id, x.EquipmentId, x.RetirementEpoch }).OnDelete(DeleteBehavior.NoAction);
    }
}
