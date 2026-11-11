namespace MedEquipSLA.Identity.Application.Abstractions;

public sealed record EmployeeView(Guid Id, string Name, string? Email, string Role, Guid? DepartmentId, bool IsActive, bool EmailConfirmed, byte[] RowVersion, bool IsInvitationSuspended = false);
public sealed record EmployeePage(IReadOnlyList<EmployeeView> Items, int Page, bool HasNext);
public sealed record EmployeeAccessInput(bool? Active, byte[]? RowVersion);
public sealed record EmployeeProfileInput(string? FullName, string? Role, Guid? DepartmentId, byte[]? RowVersion);
public enum EmployeeChangeStatus { Saved, Invalid, NotFound, Conflict, Forbidden, Unauthorized }
public interface IEmployeeStore
{
    Task<long?> TechnicianVersionAsync(Guid id, CancellationToken ct);
    Task<EmployeePage> ListAsync(string? search, int page, CancellationToken ct);
    Task<EmployeeChangeStatus> ChangeAccessAsync(Guid id, EmployeeAccessInput input, SessionUser actor, string correlation, CancellationToken ct);
    Task<EmployeeChangeStatus> ChangeProfileAsync(Guid id, EmployeeProfileInput input, string? departmentName, SessionUser actor, string correlation, CancellationToken ct);
}
public sealed class EmployeeQueries(IEmployeeStore store)
{
    public Task<long?> TechnicianVersionAsync(Guid id, CancellationToken ct) => store.TechnicianVersionAsync(id, ct);
    public Task<EmployeePage> ListAsync(string? search, int page, CancellationToken ct) => store.ListAsync(search?.Trim(), page, ct);
}
public sealed class EmployeeCommands(IEmployeeStore store)
{
    public Task<EmployeeChangeStatus> AccessAsync(Guid id, EmployeeAccessInput input, SessionUser actor, string correlation, CancellationToken ct) =>
        !actor.Roles.Contains("Admin") || input.Active is null || input.RowVersion is not { Length: 8 }
        ? Task.FromResult(EmployeeChangeStatus.Invalid) : store.ChangeAccessAsync(id, input, actor, correlation, ct);
    public Task<EmployeeChangeStatus> ProfileAsync(Guid id, EmployeeProfileInput input, string? departmentName, SessionUser actor, string correlation, CancellationToken ct) =>
        !actor.Roles.Contains("Admin") || input.FullName?.Trim().Length is not (> 0 and <= 200) || input.Role is not ("Technician" or "DepartmentStaff") ||
        input.RowVersion is not { Length: 8 } || input.DepartmentId == Guid.Empty || input.Role == "DepartmentStaff" && input.DepartmentId is null || input.DepartmentId is not null && departmentName is null
        ? Task.FromResult(EmployeeChangeStatus.Invalid) : store.ChangeProfileAsync(id, input, departmentName, actor, correlation, ct);
}
