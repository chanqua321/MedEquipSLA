using Microsoft.EntityFrameworkCore;
using MedEquipSLA.Application;
using MedEquipSLA.Equipment.Domain;
namespace MedEquipSLA.Equipment.Infrastructure.Persistence;
public sealed class EquipmentDbContext(DbContextOptions<EquipmentDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<EquipmentAsset> Records => Set<EquipmentAsset>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(EquipmentDbContext).Assembly);
}
