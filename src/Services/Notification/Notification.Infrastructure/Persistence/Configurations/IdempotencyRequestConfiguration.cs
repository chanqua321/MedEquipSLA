using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Notification.Domain;
using MedEquipSLA.Notification.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Notification.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRequestConfiguration : IEntityTypeConfiguration<IdempotencyRequest>
{
    public void Configure(EntityTypeBuilder<IdempotencyRequest> builder)
    {
        builder.ToTable("IdempotencyRequests", table =>
        {
            table.HasCheckConstraint("CK_IdempotencyRequests_State", "[State] IN (N'Pending',N'Committed',N'Aborted')");
            table.HasCheckConstraint("CK_IdempotencyRequests_Json", "[ResponseJson] IS NULL OR ISJSON([ResponseJson]) = 1");
            table.HasCheckConstraint("CK_IdempotencyRequests_Expiration", "[ExpiresAtUtc] > [CreatedAtUtc]");
        });
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
