using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Equipment.Domain;
namespace MedEquipSLA.Equipment.Infrastructure.Persistence.Configurations;
public sealed class EquipmentAssetConfiguration : IEntityTypeConfiguration<EquipmentAsset>
{
    public void Configure(EntityTypeBuilder<EquipmentAsset> builder)
    { builder.ToTable("EquipmentAssets"); builder.HasKey(x => x.Id); builder.Property(x => x.RowVersion).IsRowVersion(); }
}
