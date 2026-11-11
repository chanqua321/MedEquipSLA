using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Notification.Domain;
using MedEquipSLA.Notification.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Notification.Infrastructure.Persistence.Configurations;

public sealed class EmailDeliveryConfiguration : IEntityTypeConfiguration<EmailDelivery>
{
    public void Configure(EntityTypeBuilder<EmailDelivery> builder)
    {
        builder.ToTable("EmailDeliveries", table =>
        {
            table.HasCheckConstraint("CK_EmailDeliveries_Status", "[Status] IN (N'Pending',N'Processing',N'Sent',N'Failed')");
            table.HasCheckConstraint("CK_EmailDeliveries_AttemptCount", "[AttemptCount] >= 0");
            table.HasCheckConstraint("CK_EmailDeliveries_Sent", "([Status] = N'Sent' AND [SentAtUtc] IS NOT NULL) OR ([Status] <> N'Sent' AND [SentAtUtc] IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.NotificationId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.ToAddressSnapshot).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired(true);
        builder.Property(x => x.Subject).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(true);
        builder.Property(x => x.Body).HasColumnType("nvarchar(max)").IsRequired(true);
        builder.Property(x => x.MessageId).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired(true);
        builder.Property(x => x.Status).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.AttemptCount).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.NextAttemptAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.LeaseOwner).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.LeaseUntilUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.SentAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.LastError).HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
        builder.HasIndex(x => x.NotificationId).HasDatabaseName("UX_EmailDeliveries_NotificationId").IsUnique();
        builder.HasIndex(x => x.MessageId).HasDatabaseName("UX_EmailDeliveries_MessageId").IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc }).HasDatabaseName("IX_EmailDeliveries_Status_NextAttemptAtUtc");
        builder.HasOne<NotificationRecord>().WithOne().HasForeignKey<EmailDelivery>(x => x.NotificationId).OnDelete(DeleteBehavior.NoAction);
    }
}
