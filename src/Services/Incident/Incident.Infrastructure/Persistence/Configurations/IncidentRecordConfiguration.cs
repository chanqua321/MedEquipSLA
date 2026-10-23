using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Incident.Domain;
namespace MedEquipSLA.Incident.Infrastructure.Persistence.Configurations;
public sealed class IncidentRecordConfiguration : IEntityTypeConfiguration<IncidentRecord>
{
    public void Configure(EntityTypeBuilder<IncidentRecord> builder)
    { builder.ToTable("IncidentRecords"); builder.HasKey(x => x.Id); builder.Property(x => x.RowVersion).IsRowVersion(); }
}
