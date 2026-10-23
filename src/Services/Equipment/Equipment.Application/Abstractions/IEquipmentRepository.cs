namespace MedEquipSLA.Equipment.Application.Abstractions;
public interface IEquipmentRepository
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}
public interface IEquipmentQueryAdapter
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}
