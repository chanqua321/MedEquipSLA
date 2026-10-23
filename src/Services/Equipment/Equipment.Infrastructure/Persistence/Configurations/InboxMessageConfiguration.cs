using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence.Configurations;

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages", table =>
        {
            table.HasCheckConstraint("CK_InboxMessages_SchemaVersion", "[SchemaVersion] >= 1");
        });
        builder.HasKey(x => new { x.ConsumerName, x.EventId });
        builder.Property(x => x.ConsumerName).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired(true);
        builder.Property(x => x.EventId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.EventType).HasColumnType("nvarchar(150)").HasMaxLength(150).IsRequired(true);
        builder.Property(x => x.SchemaVersion).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.ProcessedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
    }
}
