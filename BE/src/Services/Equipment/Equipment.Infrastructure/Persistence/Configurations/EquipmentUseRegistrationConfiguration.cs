using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence.Configurations;

public sealed class EquipmentUseRegistrationConfiguration : IEntityTypeConfiguration<EquipmentUseRegistration>
{
    public void Configure(EntityTypeBuilder<EquipmentUseRegistration> builder)
    {
        builder.ToTable("EquipmentUseRegistrations", table =>
        {
            table.HasCheckConstraint("CK_EquipmentUseRegistrations_SourceService", "[SourceService] IN (N'Incident',N'Maintenance')");
            table.HasCheckConstraint("CK_EquipmentUseRegistrations_ResourceType", "[ResourceType] IN (N'Incident',N'MaintenanceSchedule',N'WorkOrder')");
            table.HasCheckConstraint("CK_EquipmentUseRegistrations_State", "[State] IN (N'Pending',N'Active',N'Released',N'Aborted')");
            table.HasCheckConstraint("CK_EquipmentUseRegistrations_SourceVersion", "[SourceVersion] >= 0");
            table.HasCheckConstraint("CK_EquipmentUseRegistrations_Fence", "[FencingToken] >= 0");
            table.HasCheckConstraint("CK_EquipmentUseRegistrations_Payload",
                "[RequestPayloadJson] IS NULL OR (ISJSON([RequestPayloadJson]) = 1 AND (([ResourceType] IN (N'WorkOrder',N'MaintenanceSchedule') AND [SourceService] = N'Maintenance') OR ([ResourceType]=N'Incident' AND [SourceService]=N'Incident')) AND [RequestHash] IS NOT NULL AND [FencingToken] > 0 AND [EquipmentCodeSnapshot] IS NOT NULL AND [EquipmentNameSnapshot] IS NOT NULL AND [DepartmentIdSnapshot] IS NOT NULL AND [DepartmentNameSnapshot] IS NOT NULL)");
            table.HasCheckConstraint("CK_EquipmentUseRegistrations_ProtocolPair",
                "([RequestHash] IS NULL AND [RequestPayloadJson] IS NULL AND [FencingToken] = 0 AND [CancelIntentId] IS NULL AND [EquipmentCodeSnapshot] IS NULL AND [EquipmentNameSnapshot] IS NULL AND [DepartmentIdSnapshot] IS NULL AND [DepartmentNameSnapshot] IS NULL) OR ([RequestHash] IS NOT NULL AND [RequestPayloadJson] IS NOT NULL)");

        });
        builder.Property(x => x.RequestHash).HasColumnType("binary(32)").HasMaxLength(32).UsePropertyAccessMode(PropertyAccessMode.Property);
        builder.Property(x => x.RequestPayloadJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.FencingToken).HasColumnType("bigint").HasDefaultValue(0L);
        builder.Property(x => x.CancelIntentId).HasColumnType("uniqueidentifier");
        builder.Property(x => x.EquipmentCodeSnapshot).HasColumnType("nvarchar(50)").HasMaxLength(50);
        builder.Property(x => x.EquipmentNameSnapshot).HasColumnType("nvarchar(200)").HasMaxLength(200);
        builder.Property(x => x.DepartmentIdSnapshot).HasColumnType("uniqueidentifier");
        builder.Property(x => x.DepartmentNameSnapshot).HasColumnType("nvarchar(200)").HasMaxLength(200);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.EquipmentId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.SourceService).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired(true);
        builder.Property(x => x.ResourceType).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired(true);
        builder.Property(x => x.ResourceId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.SourceOperationId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.State).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.SourceVersion).HasColumnType("bigint").IsRequired(true);
        builder.Property(x => x.RegisteredAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.ConfirmedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.ReleasedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.LastVerifiedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        builder.HasIndex(x => new { x.SourceService, x.ResourceType, x.ResourceId }).HasDatabaseName("UX_EquipmentUseRegistrations_SourceService_ResourceType_ResourceId").IsUnique();
        builder.HasIndex(x => new { x.SourceService, x.SourceOperationId }).HasDatabaseName("UX_EquipmentUseRegistrations_SourceService_SourceOperationId").IsUnique();
        builder.HasIndex(x => new { x.EquipmentId, x.State }).HasDatabaseName("IX_EquipmentUseRegistrations_EquipmentId_State");
        builder.HasOne<EquipmentAsset>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.NoAction);
    }
}
