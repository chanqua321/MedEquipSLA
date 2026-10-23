using Microsoft.EntityFrameworkCore;
using MedEquipSLA.Application;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence;

public sealed class EquipmentDbContext(DbContextOptions<EquipmentDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<EquipmentCategory> EquipmentCategories => Set<EquipmentCategory>();
    public DbSet<EquipmentAsset> EquipmentAssets => Set<EquipmentAsset>();
    public DbSet<EquipmentStateProjection> EquipmentStateProjections => Set<EquipmentStateProjection>();
    public DbSet<EquipmentUseRegistration> EquipmentUseRegistrations => Set<EquipmentUseRegistration>();
    public DbSet<EquipmentStatusHistory> EquipmentStatusHistories => Set<EquipmentStatusHistory>();
    public DbSet<ReconciliationCheckpoint> ReconciliationCheckpoints => Set<ReconciliationCheckpoint>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<EquipmentAsset> Records => Set<EquipmentAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EquipmentDbContext).Assembly);
    }
}
