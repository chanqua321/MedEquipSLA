using System.Security.Cryptography;
using System.Text.Json;

namespace MedEquipSLA.Incident.Application.Features.Incidents;

public sealed record CreateIncidentInput(Guid EquipmentId, string? Title, string? Description, string? Priority, Guid? RelatedIncidentId);
public enum IncidentCreationStatus { Created, Invalid, Conflict, NotFound, Unavailable }
public sealed record IncidentCreationResult(IncidentCreationStatus Status, Guid? Id = null, string? Message = null);
public sealed record PreparedIncidentCreation(Guid? OperationId = null, IncidentCreationResult? Completed = null);
public sealed record IncidentEquipmentSnapshot(Guid RegistrationId, Guid DepartmentId, string EquipmentCode, string EquipmentName, string DepartmentName);
public sealed record EquipmentRegistrationResult(IncidentCreationStatus Status, IncidentEquipmentSnapshot? Snapshot = null);
public interface IIncidentCreationStore
{
    Task<PreparedIncidentCreation> PrepareAsync(CreateIncidentInput input, Guid actor, Guid requestId, bool admin, byte[] hash, string payload, CancellationToken ct);
    Task<IncidentCreationResult> CommitAsync(CreateIncidentInput input, Guid actor, Guid operationId, bool admin, byte[] hash, IncidentEquipmentSnapshot snapshot, CancellationToken ct);
}
public interface IIncidentEquipmentRegistration
{
    Task<EquipmentRegistrationResult> RegisterAsync(Guid equipment, Guid operation, string authorization, CancellationToken ct);
}
public sealed class CreateIncidentHandler(IIncidentCreationStore store, IIncidentEquipmentRegistration equipment)
{
    public async Task<IncidentCreationResult> HandleAsync(CreateIncidentInput input, Guid actor, Guid key, bool admin, string authorization, CancellationToken ct)
    {
        var normalized = input with { Title = input.Title?.Trim(), Description = input.Description?.Trim() };
        if (actor == Guid.Empty || key == Guid.Empty || input.EquipmentId == Guid.Empty || normalized.Title?.Length is not (> 0 and <= 200) ||
            normalized.Description?.Length is not (> 0 and <= 4000) || input.Priority is not ("Critical" or "High" or "Medium" or "Low"))
            return new(IncidentCreationStatus.Invalid);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(normalized));
        var payload = JsonSerializer.Serialize(new { version = 1, request = normalized });
        var prepared = await store.PrepareAsync(normalized, actor, key, admin, hash, payload, ct);
        if (prepared.Completed is { } complete) return complete;
        if (prepared.OperationId is not { } operation) return new(IncidentCreationStatus.Unavailable);
        // Both local transactions finish before/after this call. No distributed transaction or persisted user JWT.
        var remote = await equipment.RegisterAsync(input.EquipmentId, operation, authorization, ct);
        if (remote.Snapshot is not { } snapshot) return new(remote.Status);
        return await store.CommitAsync(normalized, actor, operation, admin, hash, snapshot, ct);
    }
}
