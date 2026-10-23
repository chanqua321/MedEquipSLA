using Microsoft.EntityFrameworkCore;
using MedEquipSLA.Application;
using MedEquipSLA.Incident.Domain;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;

namespace MedEquipSLA.Incident.Infrastructure.Persistence;

public sealed class IncidentDbContext(DbContextOptions<IncidentDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<SlaPolicyVersion> SlaPolicyVersions => Set<SlaPolicyVersion>();
    public DbSet<IncidentRecord> Incidents => Set<IncidentRecord>();
    public DbSet<ResolutionAttempt> ResolutionAttempts => Set<ResolutionAttempt>();
    public DbSet<IncidentActivity> IncidentActivities => Set<IncidentActivity>();
    public DbSet<SlaDeadlineAlert> SlaDeadlineAlerts => Set<SlaDeadlineAlert>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IncidentRecord> Records => Set<IncidentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IncidentDbContext).Assembly);
    }
}
