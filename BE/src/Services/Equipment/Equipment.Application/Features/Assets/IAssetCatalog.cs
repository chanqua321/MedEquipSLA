using MedEquipSLA.Equipment.Domain;

namespace MedEquipSLA.Equipment.Application.Features.Assets;

public sealed record AssetDetail(Guid Id, string Code, Guid DepartmentId, EquipmentProfile Profile, string Status, byte[] RowVersion);
public sealed record CategoryRow(Guid Id, string Code, string Name, bool IsActive, byte[]? RowVersion = null, string? Description = null);
public enum CatalogStatus { Saved, InvalidReference, NotFound, Conflict }
public sealed record CatalogResult<T>(CatalogStatus Status, T? Value = default);

public interface IAssetCatalog
{
    Task<AssetDetail?> FindAsync(Guid id, CancellationToken ct);
    Task<CatalogResult<AssetDetail>> TransferAsync(Guid id, Guid department, byte[] version, string reason,
        Guid actor, string trace, CancellationToken ct);
    Task<IReadOnlyList<CategoryRow>> CategoriesAsync(string? search, int page, CancellationToken ct);
    Task<CatalogResult<CategoryRow>> CreateCategoryAsync(string code, string name, Guid actor, string trace, CancellationToken ct);
    Task<CategoryRow?> FindCategoryAsync(Guid id, CancellationToken ct);
    Task<CatalogResult<CategoryRow>> UpdateCategoryAsync(Guid id, string name, string? description, bool active, byte[] version,
        Guid actor, string trace, CancellationToken ct);
    Task<CatalogResult<AssetDetail>> SaveAssetAsync(Guid? id, string? code, Guid? department, byte[]? version,
        EquipmentProfile profile, Guid actor, string trace, CancellationToken ct);
}
