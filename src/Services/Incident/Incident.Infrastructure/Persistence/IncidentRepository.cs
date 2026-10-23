using Microsoft.EntityFrameworkCore;
using MedEquipSLA.Incident.Application.Abstractions;
namespace MedEquipSLA.Incident.Infrastructure.Persistence;
public sealed class IncidentRepository(IncidentDbContext db) : IIncidentRepository
{ public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => db.Records.AnyAsync(x => x.Id == id, cancellationToken); }
public sealed class IncidentQueryAdapter(IncidentDbContext db) : IIncidentQueryAdapter
{ public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => db.Records.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken); }
