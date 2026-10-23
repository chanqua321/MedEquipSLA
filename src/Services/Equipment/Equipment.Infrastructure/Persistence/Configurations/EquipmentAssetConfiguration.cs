using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence.Configurations;

public sealed class EquipmentAssetConfiguration : IEntityTypeConfiguration<EquipmentAsset>
{
    public void Configure(EntityTypeBuilder<EquipmentAsset> builder)
    {
        builder.ToTable("EquipmentAssets", table =>
        {
            table.HasCheckConstraint("CK_EquipmentAssets_AggregateVersion", "[AggregateVersion] >= 1");
            table.HasCheckConstraint("CK_EquipmentAssets_OperationalStatus", "[OperationalStatus] IN (N'Operational',N'UnderMaintenance',N'Broken',N'Retired')");
            table.HasCheckConstraint("CK_EquipmentAssets_Retirement", "([IsRetired] = 1 AND [OperationalStatus] = N'Retired' AND [RetiredAtUtc] IS NOT NULL AND [RetiredByUserId] IS NOT NULL) OR ([IsRetired] = 0 AND [OperationalStatus] <> N'Retired' AND [RetiredAtUtc] IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.AssetCode).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired(true);
        builder.Property(x => x.SerialNumber).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.Name).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(true);
        builder.Property(x => x.CategoryId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.DepartmentId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.Manufacturer).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.Model).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.PurchasedOn).HasColumnType("date").IsRequired(false);
        builder.Property(x => x.WarrantyUntil).HasColumnType("date").IsRequired(false);
        builder.Property(x => x.IsRetired).HasColumnType("bit").IsRequired(true);
        builder.Property(x => x.OperationalStatus).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired(true);
        builder.Property(x => x.RetiredAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.RetiredByUserId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.RetirementReason).HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.AggregateVersion).HasColumnType("bigint").IsRequired(true);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.CreatedByUserId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.UpdatedByUserId).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        builder.HasIndex(x => x.AssetCode).HasDatabaseName("UX_EquipmentAssets_AssetCode").IsUnique();
        builder.HasIndex(x => x.SerialNumber).HasDatabaseName("UX_EquipmentAssets_SerialNumber").IsUnique().HasFilter("[SerialNumber] IS NOT NULL");
        builder.HasIndex(x => new { x.DepartmentId, x.OperationalStatus, x.Id }).HasDatabaseName("IX_EquipmentAssets_DepartmentId_OperationalStatus_Id");
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<EquipmentCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
    }
}
