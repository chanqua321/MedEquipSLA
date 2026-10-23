using Microsoft.EntityFrameworkCore;
using MedEquipSLA.Application;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;

public sealed class MaintenanceDbContext(DbContextOptions<MaintenanceDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
    public DbSet<ScheduleOccurrence> ScheduleOccurrences => Set<ScheduleOccurrence>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<WorkOrderAttempt> WorkOrderAttempts => Set<WorkOrderAttempt>();
    public DbSet<CalibrationMeasurement> CalibrationMeasurements => Set<CalibrationMeasurement>();
    public DbSet<OccurrenceReminder> OccurrenceReminders => Set<OccurrenceReminder>();
    public DbSet<WorkOrderActivity> WorkOrderActivities => Set<WorkOrderActivity>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<WorkOrder> Records => Set<WorkOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MaintenanceDbContext).Assembly);
    }
}
