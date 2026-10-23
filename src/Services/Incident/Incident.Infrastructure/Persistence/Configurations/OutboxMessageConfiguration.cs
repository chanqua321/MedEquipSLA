using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Incident.Domain;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Incident.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", table =>
        {
            table.HasCheckConstraint("CK_OutboxMessages_Status", "[Status] IN (N'Pending',N'Processing',N'Published',N'Failed')");
            table.HasCheckConstraint("CK_OutboxMessages_Json", "ISJSON([PayloadJson]) = 1");
            table.HasCheckConstraint("CK_OutboxMessages_Versions", "[SchemaVersion] >= 1 AND [AggregateVersion] >= 1 AND [AttemptCount] >= 0");
        });
        builder.HasKey(x => x.EventId);
        builder.Property(x => x.EventId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.EventType).HasColumnType("nvarchar(150)").HasMaxLength(150).IsRequired(true);
        builder.Property(x => x.SchemaVersion).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.AggregateType).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired(true);
        builder.Property(x => x.AggregateId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.AggregateVersion).HasColumnType("bigint").IsRequired(true);
        builder.Property(x => x.OccurredAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.CorrelationId).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired(true);
        builder.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired(true);
        builder.Property(x => x.Status).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.AttemptCount).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.NextAttemptAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.LeaseOwner).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.LeaseUntilUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.PublishedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.LastError).HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired(false);
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc, x.OccurredAtUtc }).HasDatabaseName("IX_OutboxMessages_Status_NextAttemptAtUtc_OccurredAtUtc");
    }
}
