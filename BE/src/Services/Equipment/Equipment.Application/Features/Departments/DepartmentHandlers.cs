using MedEquipSLA.Equipment.Domain;

namespace MedEquipSLA.Equipment.Application.Features.Departments;

public sealed record DepartmentView(Guid Id, string Code, string Name, bool IsActive, byte[] RowVersion);
public sealed record DepartmentPage(IReadOnlyList<DepartmentView> Items, int Total, int Page, int PageSize);
public sealed record DepartmentQuery(string? Search, bool? Active, string Sort, int Page, int PageSize);
public enum DepartmentWriteStatus { Saved, NotFound, Conflict }
public sealed record DepartmentWrite(DepartmentWriteStatus Status, DepartmentView? Department = null);

public interface IDepartmentStore
{
    Task<DepartmentView?> FindAsync(Guid id, CancellationToken ct);
    Task<DepartmentPage> ListAsync(DepartmentQuery query, CancellationToken ct);
    Task<DepartmentWrite> CreateAsync(Department department, string traceId, CancellationToken ct);
    Task<DepartmentWrite> ChangeAsync(Guid id, byte[] version, string? name, Guid actor, string traceId, CancellationToken ct);
}

public sealed class DepartmentQueries(IDepartmentStore store)
{
    public Task<DepartmentView?> FindAsync(Guid id, CancellationToken ct) => store.FindAsync(id, ct);
    public Task<DepartmentPage> ListAsync(DepartmentQuery query, CancellationToken ct) => store.ListAsync(query, ct);
}

public sealed class DepartmentCommands(IDepartmentStore store)
{
    public Task<DepartmentWrite> CreateAsync(string code, string name, Guid actor, string traceId, CancellationToken ct) =>
        store.CreateAsync(Department.Create(code, name, actor, DateTimeOffset.UtcNow), traceId, ct);

    public Task<DepartmentWrite> UpdateAsync(Guid id, byte[] version, string name, Guid actor, string traceId, CancellationToken ct) =>
        store.ChangeAsync(id, version, name, actor, traceId, ct);

    public Task<DepartmentWrite> ArchiveAsync(Guid id, byte[] version, Guid actor, string traceId, CancellationToken ct) =>
        store.ChangeAsync(id, version, null, actor, traceId, ct);
}
