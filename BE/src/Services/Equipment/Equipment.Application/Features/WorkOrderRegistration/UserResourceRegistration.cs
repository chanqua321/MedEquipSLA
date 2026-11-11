namespace MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration;

public sealed record UserResourceRegistrationView(Guid RegistrationId, string EquipmentCode, string EquipmentName, Guid DepartmentId, string DepartmentName);
public sealed record UserResourceRegistrationResult(RegistrationOutcome Outcome, UserResourceRegistrationView? Value = null);
public interface IUserResourceRegistrationStore
{
    Task<UserResourceRegistrationResult> RegisterAsync(Guid equipment, Guid operation, string source, string resourceType, Guid? department, CancellationToken ct);
}
public sealed class UserResourceRegistrationHandler(IUserResourceRegistrationStore store)
{
    public Task<UserResourceRegistrationResult> HandleAsync(Guid equipment, Guid operation, string source, string resourceType, Guid? department, CancellationToken ct) =>
        equipment == Guid.Empty || operation == Guid.Empty || !(source == "Incident" && resourceType == "Incident" || source == "Maintenance" && resourceType == "MaintenanceSchedule")
        ? Task.FromResult(new UserResourceRegistrationResult(RegistrationOutcome.Invalid)) : store.RegisterAsync(equipment, operation, source, resourceType, department, ct);
}
