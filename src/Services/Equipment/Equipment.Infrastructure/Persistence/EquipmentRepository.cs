using Microsoft.EntityFrameworkCore;
using MedEquipSLA.Equipment.Application.Abstractions;
namespace MedEquipSLA.Equipment.Infrastructure.Persistence;
public sealed class EquipmentRepository(EquipmentDbContext db) : IEquipmentRepository
{ public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => db.Records.AnyAsync(x => x.Id == id, cancellationToken); }
public sealed class EquipmentQueryAdapter(EquipmentDbContext db) : IEquipmentQueryAdapter
{ public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => db.Records.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken); }
