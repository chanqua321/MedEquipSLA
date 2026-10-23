using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Notification.Domain;
namespace MedEquipSLA.Notification.Infrastructure.Persistence.Configurations;
public sealed class NotificationRecordConfiguration : IEntityTypeConfiguration<NotificationRecord>
{
    public void Configure(EntityTypeBuilder<NotificationRecord> builder)
    { builder.ToTable("NotificationRecords"); builder.HasKey(x => x.Id); builder.Property(x => x.RowVersion).IsRowVersion(); }
}
