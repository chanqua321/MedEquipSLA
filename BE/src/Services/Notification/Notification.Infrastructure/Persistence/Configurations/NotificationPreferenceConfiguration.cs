using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Notification.Domain;
using MedEquipSLA.Notification.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Notification.Infrastructure.Persistence.Configurations;

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");
        builder.HasKey(x => new { x.UserId, x.NotificationKind });
        builder.Property(x => x.UserId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.NotificationKind).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired(true);
        builder.Property(x => x.InAppEnabled).HasColumnType("bit").IsRequired(true);
        builder.Property(x => x.EmailEnabled).HasColumnType("bit").IsRequired(true);
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.RowVersion).HasColumnType("rowversion").IsRowVersion();
    }
}
