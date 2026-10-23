using Microsoft.EntityFrameworkCore;
using MedEquipSLA.Application;
using MedEquipSLA.Incident.Domain;
namespace MedEquipSLA.Incident.Infrastructure.Persistence;
public sealed class IncidentDbContext(DbContextOptions<IncidentDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<IncidentRecord> Records => Set<IncidentRecord>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(IncidentDbContext).Assembly);
}
