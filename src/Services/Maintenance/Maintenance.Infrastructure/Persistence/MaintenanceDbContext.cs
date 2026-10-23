using Microsoft.EntityFrameworkCore;
using MedEquipSLA.Application;
using MedEquipSLA.Maintenance.Domain;
namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;
public sealed class MaintenanceDbContext(DbContextOptions<MaintenanceDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<WorkOrder> Records => Set<WorkOrder>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(MaintenanceDbContext).Assembly);
}
