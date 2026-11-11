using System.Security.Cryptography;
using MedEquipSLA.Equipment.Domain;

namespace MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration;

public enum RegistrationOutcome { Saved, Absent, NotFound, Invalid, Conflict, Retired, AdmissionClosed, StaleFence, Terminal }
public sealed record RegistrationView(Guid Id, Guid EquipmentId, Guid OccurrenceId, Guid OperationId,
    string State, long Fence, byte[] Hash, string EquipmentCode, string EquipmentName,
    Guid DepartmentId, string DepartmentName, long SourceVersion, Guid? CancelIntentId);
public sealed record RegistrationResult(RegistrationOutcome Outcome, RegistrationView? Registration = null);

public interface IWorkOrderRegistrationStore
{
    Task<RegistrationResult> RegisterAsync(WorkOrderRegistrationRequest request, Guid operationId,
        long fence, string correlation, CancellationToken ct);
    Task<RegistrationResult> FindAsync(Guid equipment, Guid occurrence, Guid operation, byte[] hash, CancellationToken ct);
    Task<RegistrationResult> ConfirmAsync(WorkOrderRegistrationRequest request, Guid operation,
        Guid registrationId, long fence, long sourceVersion, string correlation, CancellationToken ct);
    Task<RegistrationResult> AbortAsync(WorkOrderRegistrationRequest request, Guid operation,
        long fence, Guid cancelIntent, string correlation, CancellationToken ct);
}

public sealed class WorkOrderRegistrationHandler(IWorkOrderRegistrationStore store)
{
    public Task<RegistrationResult> RegisterAsync(WorkOrderRegistrationRequest request, Guid operation,
        long fence, byte[] hash, string correlation, CancellationToken ct)
    {
        if (operation == Guid.Empty || fence <= 0 || hash.Length != 32 || correlation.Length > 100)
            return Task.FromResult(new RegistrationResult(RegistrationOutcome.Invalid));
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(request.Hash(), hash))
                return Task.FromResult(new RegistrationResult(RegistrationOutcome.Conflict));
        }
        catch (InvalidOperationException) { return Task.FromResult(new RegistrationResult(RegistrationOutcome.Invalid)); }
        return store.RegisterAsync(request, operation, fence, correlation, ct);
    }

    public Task<RegistrationResult> ConfirmAsync(WorkOrderRegistrationRequest request, Guid operation,
        Guid registrationId, long fence, long sourceVersion, byte[] hash, string correlation, CancellationToken ct)
    {
        if (registrationId == Guid.Empty || sourceVersion < 1)
            return Task.FromResult(new RegistrationResult(RegistrationOutcome.Invalid));
        var invalid = ValidateMutation(request, operation, fence, hash, correlation);
        return invalid is not null ? Task.FromResult(invalid) :
            store.ConfirmAsync(request, operation, registrationId, fence, sourceVersion, correlation, ct);
    }

    public Task<RegistrationResult> AbortAsync(WorkOrderRegistrationRequest request, Guid operation,
        long fence, Guid cancelIntent, byte[] hash, string correlation, CancellationToken ct)
    {
        if (cancelIntent == Guid.Empty) return Task.FromResult(new RegistrationResult(RegistrationOutcome.Invalid));
        var invalid = ValidateMutation(request, operation, fence, hash, correlation);
        return invalid is not null ? Task.FromResult(invalid) :
            store.AbortAsync(request, operation, fence, cancelIntent, correlation, ct);
    }

    private static RegistrationResult? ValidateMutation(WorkOrderRegistrationRequest request, Guid operation,
        long fence, byte[] hash, string correlation)
    {
        if (operation == Guid.Empty || fence < 1 || hash.Length != 32 || correlation.Length > 100)
            return new(RegistrationOutcome.Invalid);
        try { return CryptographicOperations.FixedTimeEquals(request.Hash(), hash) ? null : new(RegistrationOutcome.Conflict); }
        catch (InvalidOperationException) { return new(RegistrationOutcome.Invalid); }
    }

    public Task<RegistrationResult> FindAsync(Guid equipment, Guid occurrence, Guid operation, byte[] hash, CancellationToken ct) =>
        equipment == Guid.Empty || occurrence == Guid.Empty || operation == Guid.Empty || hash.Length != 32
            ? Task.FromResult(new RegistrationResult(RegistrationOutcome.Invalid))
            : store.FindAsync(equipment, occurrence, operation, hash, ct);
}
