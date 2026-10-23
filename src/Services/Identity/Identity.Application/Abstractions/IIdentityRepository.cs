namespace MedEquipSLA.Identity.Application.Abstractions;
public interface IIdentityRepository
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}
public interface IIdentityQueryAdapter
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}
