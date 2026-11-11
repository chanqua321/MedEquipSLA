using System.Data;
using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Equipment.Application.Features.Assets;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence;

public sealed class AssetCatalogStore(EquipmentDbContext db) : IAssetCatalog
{
    public async Task<AssetDetail?> FindAsync(Guid id, CancellationToken ct)
    {
        var asset = await db.EquipmentAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return asset is null ? null : View(asset);
    }

    public async Task<IReadOnlyList<CategoryRow>> CategoriesAsync(string? search, int page, CancellationToken ct)
    {
        const int size = 20;
        var query = db.EquipmentCategories.AsNoTracking();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(x => x.Name.Contains(search) || x.Code.Contains(search));
        return await query.OrderBy(x => x.Code).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new CategoryRow(x.Id, x.Code, x.Name, x.IsActive, x.RowVersion, x.Description)).ToListAsync(ct);
    }

    public async Task<CatalogResult<CategoryRow>> CreateCategoryAsync(string code, string name, Guid actor,
        string trace, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var category = EquipmentCategory.Create(code, name, actor, now);
        db.EquipmentCategories.Add(category);
        var row = new CategoryRow(category.Id, code, name, true);
        Audit(actor, "EquipmentCategoryCreated", "EquipmentCategory", category.Id, now, trace, row);
        return await SaveAsync(ct) ? new(CatalogStatus.Saved, new(category.Id, category.Code, category.Name, category.IsActive,
            category.RowVersion, category.Description)) : new(CatalogStatus.Conflict);
    }

    public Task<CategoryRow?> FindCategoryAsync(Guid id, CancellationToken ct) =>
        db.EquipmentCategories.AsNoTracking().Where(x => x.Id == id).Select(x =>
            new CategoryRow(x.Id, x.Code, x.Name, x.IsActive, x.RowVersion, x.Description)).SingleOrDefaultAsync(ct);

    public async Task<CatalogResult<CategoryRow>> UpdateCategoryAsync(Guid id, string name, string? description, bool active,
        byte[] version, Guid actor, string trace, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await db.EquipmentCategories.FromSqlInterpolated(
            $"SELECT * FROM dbo.EquipmentCategories WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleOrDefaultAsync(ct);
        if (row is null) return new(CatalogStatus.NotFound);
        await db.Entry(row).ReloadAsync(ct);
        if (!version.SequenceEqual(row.RowVersion)) return new(CatalogStatus.Conflict);
        var before = new { row.Name, row.Description, row.IsActive };
        row.Revise(name, description, active, actor, DateTimeOffset.UtcNow);
        Audit(actor, active ? "EquipmentCategoryUpdated" : "EquipmentCategoryArchived", "EquipmentCategory", id,
            DateTimeOffset.UtcNow, trace, new { before, after = new { row.Name, row.Description, row.IsActive } });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(CatalogStatus.Saved, new(row.Id, row.Code, row.Name, row.IsActive, row.RowVersion, row.Description));
    }

    public async Task<CatalogResult<AssetDetail>> SaveAssetAsync(Guid? id, string? code, Guid? department, byte[]? version,
        EquipmentProfile profile, Guid actor, string trace, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var now = DateTimeOffset.UtcNow;
        EquipmentAsset asset;
        EquipmentProfile? previous = null;
        if (id is { } existingId)
        {
            var existing = await db.EquipmentAssets.FromSqlInterpolated(
                $"SELECT * FROM dbo.EquipmentAssets WITH (UPDLOCK, HOLDLOCK) WHERE Id = {existingId}").SingleOrDefaultAsync(ct);
            if (existing is null) return new(CatalogStatus.NotFound);
            if (existing.IsRetired || version is null || !version.SequenceEqual(existing.RowVersion))
                return new(CatalogStatus.Conflict);
            if (existing.CategoryId != profile.CategoryId && !await IsActiveCategoryAsync(profile.CategoryId, ct))
                return new(CatalogStatus.InvalidReference);
            asset = existing;
            previous = View(asset).Profile;
            asset.UpdateProfile(profile, actor, now);
        }
        else
        {
            if (department is not { } unit || code is null ||
                !await db.Departments.AnyAsync(x => x.Id == unit && x.IsActive, ct) ||
                !await IsActiveCategoryAsync(profile.CategoryId, ct))
                return new(CatalogStatus.InvalidReference);
            asset = EquipmentAsset.Register(code, unit, profile, actor, now);
            db.EquipmentAssets.Add(asset);
        }

        Audit(actor, id is null ? "EquipmentCreated" : "EquipmentUpdated", "EquipmentAsset", asset.Id, now, trace,
            new { asset.AssetCode, asset.DepartmentId, previous, profile });
        if (!await SaveAsync(ct)) return new(CatalogStatus.Conflict);
        await transaction.CommitAsync(ct);
        return new(CatalogStatus.Saved, View(asset));
    }

    public async Task<CatalogResult<AssetDetail>> TransferAsync(Guid id, Guid department, byte[] version, string reason,
        Guid actor, string trace, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var asset = await db.EquipmentAssets.FromSqlInterpolated(
            $"SELECT * FROM dbo.EquipmentAssets WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}").SingleOrDefaultAsync(ct);
        if (asset is null) return new(CatalogStatus.NotFound);
        if (asset.IsRetired || asset.DepartmentId == department || !version.SequenceEqual(asset.RowVersion))
            return new(CatalogStatus.Conflict);
        var target = await db.Departments.SingleOrDefaultAsync(x => x.Id == department && x.IsActive, ct);
        if (target is null) return new(CatalogStatus.InvalidReference);
        var previous = await db.Departments.SingleAsync(x => x.Id == asset.DepartmentId, ct);
        var now = DateTimeOffset.UtcNow;
        var eventId = Guid.NewGuid();
        asset.Transfer(target.Id, actor, now);
        var payload = new EquipmentDepartmentChanged(previous.Id, previous.Name, target.Id, target.Name);
        db.EquipmentStatusHistories.Add(EquipmentStatusHistory.Transfer(id, previous, target, actor, eventId, reason, now));
        db.OutboxMessages.Add(new OutboxMessage(new IntegrationEvent(eventId, EquipmentDepartmentChanged.EventType,
            1, "Equipment", id, asset.AggregateVersion, now, trace, JsonSerializer.Serialize(payload))));
        Audit(actor, "EquipmentDepartmentChanged", "EquipmentAsset", id, now, trace, new { payload, reason });
        if (!await SaveAsync(ct)) return new(CatalogStatus.Conflict);
        await transaction.CommitAsync(ct);
        return new(CatalogStatus.Saved, View(asset));
    }

    private Task<bool> IsActiveCategoryAsync(Guid id, CancellationToken ct) =>
        db.EquipmentCategories.AnyAsync(x => x.Id == id && x.IsActive, ct);

    private void Audit<T>(Guid actor, string action, string type, Guid id, DateTimeOffset now, string trace, T changes) =>
        db.AuditEntries.Add(new AuditEntry(actor, action, type, id, now, trace, JsonSerializer.Serialize(changes)));

    private async Task<bool> SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        { return false; }
    }

    private static AssetDetail View(EquipmentAsset asset) =>
        new(asset.Id, asset.AssetCode, asset.DepartmentId, new EquipmentProfile(asset.Name, asset.SerialNumber,
            asset.CategoryId, asset.Manufacturer, asset.Model, asset.PurchasedOn, asset.WarrantyUntil),
            asset.OperationalStatus, asset.RowVersion);
}
