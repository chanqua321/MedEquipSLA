using Microsoft.EntityFrameworkCore;
using MedEquipSLA.Identity.Application.Abstractions;
namespace MedEquipSLA.Identity.Infrastructure.Persistence;
public sealed class IdentityRepository(IdentityDbContext db) : IIdentityRepository
{ public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => db.Users.AnyAsync(x => x.Id == id, cancellationToken); }
public sealed class IdentityQueryAdapter(IdentityDbContext db) : IIdentityQueryAdapter
{ public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => db.Users.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken); }
