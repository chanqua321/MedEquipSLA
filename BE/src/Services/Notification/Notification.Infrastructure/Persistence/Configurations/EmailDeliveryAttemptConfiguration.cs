using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MedEquipSLA.Notification.Domain;
using MedEquipSLA.Notification.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Notification.Infrastructure.Persistence.Configurations;

public sealed class EmailDeliveryAttemptConfiguration : IEntityTypeConfiguration<EmailDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<EmailDeliveryAttempt> builder)
    {
        builder.ToTable("EmailDeliveryAttempts", table =>
        {
            table.HasCheckConstraint("CK_EmailDeliveryAttempts_AttemptNumber", "[AttemptNumber] > 0");
            table.HasCheckConstraint("CK_EmailDeliveryAttempts_Outcome", "[Outcome] IN (N'Processing',N'Sent',N'Failed',N'Unknown')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.EmailDeliveryId).HasColumnType("uniqueidentifier").IsRequired(true);
        builder.Property(x => x.AttemptNumber).HasColumnType("int").IsRequired(true);
        builder.Property(x => x.StartedAtUtc).HasColumnType("datetimeoffset").IsRequired(true);
        builder.Property(x => x.FinishedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(x => x.Outcome).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired(true);
        builder.Property(x => x.SmtpResponse).HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.ErrorCode).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired(false);
        builder.HasIndex(x => new { x.EmailDeliveryId, x.AttemptNumber }).HasDatabaseName("UX_EmailDeliveryAttempts_EmailDeliveryId_AttemptNumber").IsUnique();
        builder.HasOne<EmailDelivery>().WithMany().HasForeignKey(x => x.EmailDeliveryId).OnDelete(DeleteBehavior.NoAction);
    }
}
