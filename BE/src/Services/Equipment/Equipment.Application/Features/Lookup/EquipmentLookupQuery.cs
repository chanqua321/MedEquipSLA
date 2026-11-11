namespace MedEquipSLA.Equipment.Application.Features.Lookup;

public sealed record EquipmentLookupView(Guid Id, string Code, Guid DepartmentId, string Status, long Version);

public sealed record EquipmentListItem(Guid Id, string Code, string Name, string Department, string Status);
public sealed record EquipmentPage(IReadOnlyList<EquipmentListItem> Items, bool HasNext);

public interface IEquipmentLookupReader
{
    Task<EquipmentPage> ListAsync(Guid? department, string? search, int page, CancellationToken ct);
    Task<EquipmentLookupView?> FindAsync(Guid id, Guid? department, CancellationToken ct);
}

public sealed class EquipmentLookupQuery(IEquipmentLookupReader reader)
{
    public Task<EquipmentPage> ListAsync(Guid? department, string? search, int page, CancellationToken ct) =>
        reader.ListAsync(department, search, page, ct);

    public Task<EquipmentLookupView?> Handle(Guid id, Guid? department, CancellationToken ct) =>
        reader.FindAsync(id, department, ct);
}
