using Microsoft.EntityFrameworkCore;
using MedEquipSLA.Maintenance.Application.Abstractions;
namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;
public sealed class MaintenanceRepository(MaintenanceDbContext db) : IMaintenanceRepository
{ public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => db.Records.AnyAsync(x => x.Id == id, cancellationToken); }
public sealed class MaintenanceQueryAdapter(MaintenanceDbContext db) : IMaintenanceQueryAdapter
{ public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => db.Records.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken); }
