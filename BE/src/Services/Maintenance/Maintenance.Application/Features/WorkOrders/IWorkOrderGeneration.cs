namespace MedEquipSLA.Maintenance.Application.Features.WorkOrders;

public interface IWorkOrderGeneration
{
    Task<int> RunBatchAsync(CancellationToken ct);
    Task<bool> ProcessAsync(Guid operation, CancellationToken ct);
}
