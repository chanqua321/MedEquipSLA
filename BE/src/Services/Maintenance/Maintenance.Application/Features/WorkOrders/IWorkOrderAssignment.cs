namespace MedEquipSLA.Maintenance.Application.Features.WorkOrders;

public enum AssignmentResult { Saved, NotFound, Conflict, Invalid, Unavailable }

public interface IWorkOrderAssignment
{
    Task<AssignmentResult> AssignAsync(Guid id, Guid technician, byte[] rowVersion, Guid actor, CancellationToken ct, long authorizationVersion = 0);
}
public interface ITechnicianEligibility
{
    Task<long> FindVersionAsync(Guid technician, string authorization, CancellationToken ct);
}
public sealed class WorkOrderAssignmentCommand(IWorkOrderAssignment store, ITechnicianEligibility technicians)
{
    public async Task<AssignmentResult> HandleAsync(Guid id, Guid technician, byte[]? version, Guid actor, string authorization, CancellationToken ct)
    {
        if (actor == Guid.Empty || technician == Guid.Empty || version is not { Length: 8 }) return AssignmentResult.Invalid;
        var current = await technicians.FindVersionAsync(technician, authorization, ct);
        return current > 0 ? await store.AssignAsync(id, technician, version, actor, ct, current) :
            current == -1 ? AssignmentResult.Invalid : AssignmentResult.Unavailable;
    }
}
