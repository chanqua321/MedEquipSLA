namespace MedEquipSLA.Incident.Application.Abstractions;
public interface IIncidentRepository
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}
public interface IIncidentQueryAdapter
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}
