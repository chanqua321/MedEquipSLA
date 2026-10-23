using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Incident.Domain;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Incident.Infrastructure.Persistence.Configurations;

public sealed class SlaPolicyConfiguration : IEntityTypeConfiguration<SlaPolicy>
{
    public void Configure(EntityTypeBuilder<SlaPolicy> builder)
    {
        builder.ToTable("SlaPolicies", table =>
        {
            table.HasCheckConstraint("CK_SlaPolicies_Priority", "[Priority] IN (N'Critical',N'High',N'Medium',N'Low')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.Priority).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.Name).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(true);
        builder.Property(x => x.IsEnabled).HasColumnType("bit").IsRequired(true);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.CreatedByUserId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.UpdatedByUserId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        builder.HasIndex(x => x.Priority).HasDatabaseName("UX_SlaPolicies_Priority").IsUnique();
    }
}
