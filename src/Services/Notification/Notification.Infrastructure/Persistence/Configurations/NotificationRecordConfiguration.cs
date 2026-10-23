using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Notification.Domain;
using MedEquipSLA.Notification.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Notification.Infrastructure.Persistence.Configurations;

public sealed class NotificationRecordConfiguration : IEntityTypeConfiguration<NotificationRecord>
{
    public void Configure(EntityTypeBuilder<NotificationRecord> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.RecipientUserId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.SourceEventId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.Kind).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired(true);
        builder.Property(x => x.Title).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(true);
        builder.Property(x => x.Body).HasColumnType("nvarchar(4000)").HasMaxLength(4000).IsRequired(true);
        builder.Property(x => x.ResourceType).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired(true);
        builder.Property(x => x.ResourceId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.DepartmentIdSnapshot).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.ReadAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        builder.HasIndex(x => new { x.SourceEventId, x.RecipientUserId, x.Kind }).HasDatabaseName("UX_Notifications_SourceEventId_RecipientUserId_Kind").IsUnique();
        builder.HasIndex(x => new { x.RecipientUserId, x.ReadAtUtc, x.CreatedAtUtc, x.Id }).HasDatabaseName("IX_Notifications_RecipientUserId_ReadAtUtc_CreatedAtUtc_Id");
    }
}
