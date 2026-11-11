using MedEquipSLA.Equipment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence.Configurations;

public sealed class EquipmentRetirementIntentConfiguration : IEntityTypeConfiguration<EquipmentRetirementIntent>
{
    public void Configure(EntityTypeBuilder<EquipmentRetirementIntent> b)
    {
        b.ToTable("EquipmentRetirementIntents", t =>
        {
            t.HasCheckConstraint("CK_RetirementIntents_State", "[State] IN (N'Coordinating',N'Ready',N'Cancelling',N'Cancelled',N'Completed')");
            t.HasCheckConstraint("CK_RetirementIntents_Epoch", "[RetirementEpoch] > 0 AND [ContinueCount] >= 0");
            t.HasCheckConstraint("CK_RetirementIntents_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
            t.HasCheckConstraint("CK_RetirementIntents_Coverage", "[RequiredSourcesJson] = N'{\"Maintenance\":1,\"Incident\":1}'");
            t.HasCheckConstraint("CK_RetirementIntents_Completion", "([State] = N'Completed' AND [CompletedAtUtc] IS NOT NULL) OR ([State] <> N'Completed' AND [CompletedAtUtc] IS NULL)");
            t.HasCheckConstraint("CK_RetirementIntents_Cancellation", "([State] IN (N'Cancelling',N'Cancelled') AND [CancelRequestedByUserId] IS NOT NULL AND [CancelRequestedAtUtc] IS NOT NULL AND [CancelReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CancelReason]))) > 0) OR ([State] NOT IN (N'Cancelling',N'Cancelled') AND [CancelRequestedByUserId] IS NULL AND [CancelRequestedAtUtc] IS NULL AND [CancelReason] IS NULL)");
            t.HasCheckConstraint("CK_RetirementIntents_Cancelled", "([State] = N'Cancelled' AND [CancelledAtUtc] IS NOT NULL) OR ([State] <> N'Cancelled' AND [CancelledAtUtc] IS NULL)");
            t.HasCheckConstraint("CK_RetirementIntents_Utc", "DATEPART(TZOFFSET,[CreatedAtUtc])=0 AND DATEPART(TZOFFSET,[UpdatedAtUtc])=0 AND [UpdatedAtUtc] >= [CreatedAtUtc]");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.Id, x.EquipmentId, x.RetirementEpoch });
        b.Ignore(x => x.IsActive);
        b.Property(x => x.Id).HasColumnType("uniqueidentifier");
        b.Property(x => x.EquipmentId).HasColumnType("uniqueidentifier");
        b.Property(x => x.RetirementEpoch).HasColumnType("bigint");
        b.Property(x => x.ActorUserId).HasColumnType("uniqueidentifier");
        b.Property(x => x.RequestKey).HasColumnType("uniqueidentifier");
        b.Property(x => x.RequestHash).HasColumnType("binary(32)").HasMaxLength(32).IsRequired();
        b.Property(x => x.Reason).HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired();
        b.Property(x => x.RequiredSourcesJson).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        b.Property(x => x.State).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset");
        b.Property(x => x.UpdatedAtUtc).HasColumnType("datetimeoffset");
        b.Property(x => x.CancelRequestedByUserId).HasColumnType("uniqueidentifier");
        b.Property(x => x.CancelReason).HasColumnType("nvarchar(2000)").HasMaxLength(2000);
        b.Property(x => x.CancelRequestedAtUtc).HasColumnType("datetimeoffset");
        b.Property(x => x.CancelledAtUtc).HasColumnType("datetimeoffset");
        b.Property(x => x.CompletedAtUtc).HasColumnType("datetimeoffset");
        b.Property(x => x.ContinueCount).HasColumnType("int");
        b.Property(x => x.LastContinuedByUserId).HasColumnType("uniqueidentifier");
        b.Property(x => x.LastContinueReason).HasColumnType("nvarchar(2000)").HasMaxLength(2000);
        b.Property(x => x.LastContinuedAtUtc).HasColumnType("datetimeoffset");
        b.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        b.HasIndex(x => new { x.EquipmentId, x.RetirementEpoch }).IsUnique();
        b.HasIndex(x => x.EquipmentId).IsUnique().HasDatabaseName("UX_RetirementIntents_ActiveEquipment")
            .HasFilter("[State] <> N'Cancelled' AND [State] <> N'Completed'");
        b.HasIndex(x => new { x.ActorUserId, x.RequestKey }).IsUnique();
        b.HasIndex(x => new { x.State, x.CreatedAtUtc, x.Id });
        b.HasOne<EquipmentAsset>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.NoAction);
        b.Navigation(x => x.Receipts).HasField("receipts").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
