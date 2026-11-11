using MedEquipSLA.Equipment.Domain;

namespace MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration;

public sealed record ResourceUseProof(Guid RegistrationId, Guid EquipmentId, Guid OperationId, string State,
    long Fence, Guid? CancelIntentId, byte[]? RequestHash, long SourceVersion);
public interface IResourceRegistrationStore
{
    Task<ResourceUseProof?> AbortAsync(ResourceUseAbortRequest request, Guid cancel, long fence, string correlation, CancellationToken ct);
    Task<ResourceUseProof?> ReleaseScheduleAsync(Guid equipment, Guid schedule, Guid registration, long version, string correlation, CancellationToken ct);
}
