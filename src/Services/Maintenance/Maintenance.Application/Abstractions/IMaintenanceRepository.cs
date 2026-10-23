namespace MedEquipSLA.Maintenance.Application.Abstractions;
public interface IMaintenanceRepository
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}
public interface IMaintenanceQueryAdapter
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}
