using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Incident.Domain;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Incident.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRequestConfiguration : IEntityTypeConfiguration<IdempotencyRequest>
{
    public void Configure(EntityTypeBuilder<IdempotencyRequest> builder)
    {
        builder.ToTable("IdempotencyRequests", table =>
        {
            table.HasCheckConstraint("CK_IdempotencyRequests_State", "[State] IN (N'Pending',N'Committed',N'Aborted')");
            table.HasCheckConstraint("CK_IdempotencyRequests_Json", "[ResponseJson] IS NULL OR ISJSON([ResponseJson]) = 1");
            table.HasCheckConstraint("CK_IdempotencyRequests_Expiration", "[ExpiresAtUtc] > [CreatedAtUtc]");
            table.HasCheckConstraint("CK_IdempotencyRequests_RequestPayload", "[RequestPayloadJson] IS NULL OR ISJSON([RequestPayloadJson])=1");
            table.HasCheckConstraint("CK_IdempotencyRequests_D3Counters", "[AttemptCount]>=0 AND [TotalAttemptCount]>=[AttemptCount] AND [FencingToken]>=0");
            table.HasCheckConstraint("CK_IdempotencyRequests_Lease", "([LeaseOwner] IS NULL AND [LeaseUntilUtc] IS NULL) OR ([LeaseOwner] IS NOT NULL AND [LeaseUntilUtc] IS NOT NULL AND [CurrentAttemptId] IS NOT NULL AND [FencingToken]>0)");
            table.HasCheckConstraint("CK_IdempotencyRequests_Cancellation", "([CancelIntentId] IS NULL AND [CancelRequestedByUserId] IS NULL AND [CancelReason] IS NULL) OR ([CancelIntentId] IS NOT NULL AND [CancelRequestedByUserId] IS NOT NULL AND LEN([CancelReason])>0)");
        });
        builder.Property(x => x.EquipmentId).HasColumnType("uniqueidentifier");
        builder.Property(x => x.AttemptCount).HasColumnType("int").HasDefaultValue(0);
        builder.Property(x => x.TotalAttemptCount).HasColumnType("bigint").HasDefaultValue(0L);
        builder.Property(x => x.CurrentAttemptId).HasColumnType("uniqueidentifier");
        builder.Property(x => x.NextRetryAtUtc).HasColumnType("datetimeoffset");
        builder.Property(x => x.LastAttemptAtUtc).HasColumnType("datetimeoffset");
        builder.Property(x => x.LastError).HasColumnType("nvarchar(1000)").HasMaxLength(1000);
        builder.Property(x => x.LeaseOwner).HasColumnType("nvarchar(128)").HasMaxLength(128);
        builder.Property(x => x.LeaseUntilUtc).HasColumnType("datetimeoffset");
        builder.Property(x => x.FencingToken).HasColumnType("bigint").HasDefaultValue(0L);
        builder.Property(x => x.InterventionRequired).HasColumnType("bit").HasDefaultValue(false);
        builder.Property(x => x.CancelIntentId).HasColumnType("uniqueidentifier");
        builder.Property(x => x.CancelRequestedByUserId).HasColumnType("uniqueidentifier");
        builder.Property(x => x.CancelReason).HasColumnType("nvarchar(2000)").HasMaxLength(2000);
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetimeoffset");
        builder.HasIndex(x => new { x.State, x.InterventionRequired, x.NextRetryAtUtc, x.OperationId });
        builder.Property(x => x.RequestPayloadJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.EquipmentId, x.State, x.OperationName });
        builder.HasKey(x => x.OperationId);
        builder.Property(x => x.OperationId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.ActorScope).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired(true);
        builder.Property(x => x.OperationName).HasColumnType("nvarchar(80)").HasMaxLength(80).IsRequired(true);
        builder.Property(x => x.IdempotencyKey).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired(true);
        builder.Property(x => x.RequestHash).HasColumnType("binary(32)").HasMaxLength(32).IsRequired(true);
        builder.Property(x => x.ResourceId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.State).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.ResponseStatusCode).HasColumnType("int").IsRequired(false);
        builder.Property(x => x.ResponseJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.ExpiresAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        builder.HasIndex(x => new { x.ActorScope, x.OperationName, x.IdempotencyKey }).HasDatabaseName("UX_IdempotencyRequests_ActorScope_OperationName_IdempotencyKey").IsUnique();
    }
}
