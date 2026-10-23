using Microsoft.EntityFrameworkCore;
using MedEquipSLA.Application;
using MedEquipSLA.Notification.Domain;
using MedEquipSLA.Notification.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Notification.Infrastructure.Persistence;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<EmailDelivery> EmailDeliveries => Set<EmailDelivery>();
    public DbSet<EmailDeliveryAttempt> EmailDeliveryAttempts => Set<EmailDeliveryAttempt>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<NotificationRecord> Records => Set<NotificationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
    }
}
