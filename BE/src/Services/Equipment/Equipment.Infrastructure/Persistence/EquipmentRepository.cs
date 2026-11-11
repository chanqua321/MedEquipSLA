using Microsoft.EntityFrameworkCore;
using MedEquipSLA.Equipment.Application.Features.Lookup;
namespace MedEquipSLA.Equipment.Infrastructure.Persistence;
public sealed class EquipmentQueryAdapter(EquipmentDbContext db) : IEquipmentLookupReader
{
    public async Task<EquipmentPage> ListAsync(Guid? department, string? search, int page, CancellationToken ct)
    {
        const int pageSize = 20;
        var assets = db.EquipmentAssets.AsNoTracking()
            .Where(x => department == null || x.DepartmentId == department);
        if (!string.IsNullOrEmpty(search))
            assets = assets.Where(x => x.AssetCode.Contains(search) || x.Name.Contains(search));

        var items = await (from asset in assets
                           join unit in db.Departments on asset.DepartmentId equals unit.Id
                           orderby asset.AssetCode, asset.Id
                           select new { asset.Id, asset.AssetCode, asset.Name, Department = unit.Name, asset.OperationalStatus })
            .Skip((page - 1) * pageSize).Take(pageSize + 1).ToListAsync(ct);
        var pageItems = items.Take(pageSize)
            .Select(x => new EquipmentListItem(x.Id, x.AssetCode, x.Name, x.Department, x.OperationalStatus)).ToArray();
        return new(pageItems, items.Count > pageSize);
    }

    public Task<EquipmentLookupView?> FindAsync(Guid id, Guid? department, CancellationToken ct) =>
        db.EquipmentAssets.AsNoTracking()
            .Where(x => x.Id == id && (department == null || x.DepartmentId == department))
            .Select(x => new EquipmentLookupView(x.Id, x.AssetCode, x.DepartmentId, x.OperationalStatus, x.AggregateVersion))
            .SingleOrDefaultAsync(ct);
}
