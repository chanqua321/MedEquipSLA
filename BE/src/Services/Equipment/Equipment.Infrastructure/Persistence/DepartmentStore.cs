using System.Text.Json;
using MedEquipSLA.Equipment.Application.Features.Departments;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence;

public sealed class DepartmentStore(EquipmentDbContext db) : IDepartmentStore
{
    public async Task<DepartmentView?> FindAsync(Guid id, CancellationToken ct)
    {
        var department = await db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return department is null ? null : View(department);
    }

    public async Task<DepartmentPage> ListAsync(DepartmentQuery query, CancellationToken ct)
    {
        var rows = db.Departments.AsNoTracking();
        if (query.Active is bool active)
            rows = rows.Where(x => x.IsActive == active);
        if (query.Search is { Length: > 0 } search)
            rows = rows.Where(x => x.Name.Contains(search) || x.Code.Contains(search));

        var total = await rows.CountAsync(ct);
        var ordered = query.Sort switch
        {
            "-name" => rows.OrderByDescending(x => x.Name),
            "code" => rows.OrderBy(x => x.Code),
            "-code" => rows.OrderByDescending(x => x.Code),
            _ => rows.OrderBy(x => x.Name)
        };
        var items = await ordered.ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new DepartmentView(x.Id, x.Code, x.Name, x.IsActive, x.RowVersion))
            .ToListAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }

    public async Task<DepartmentWrite> CreateAsync(Department department, string traceId, CancellationToken ct)
    {
        db.Departments.Add(department);
        Audit(department, "DepartmentCreated", traceId);
        try
        {
            await db.SaveChangesAsync(ct);
            return new(DepartmentWriteStatus.Saved, View(department));
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return new(DepartmentWriteStatus.Conflict);
        }
    }

    public async Task<DepartmentWrite> ChangeAsync(Guid id, byte[] version, string? name, Guid actor, string traceId, CancellationToken ct)
    {
        var department = await db.Departments.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (department is null)
            return new(DepartmentWriteStatus.NotFound);
        if (!department.RowVersion.AsSpan().SequenceEqual(version))
            return new(DepartmentWriteStatus.Conflict);
        if (name is null && !department.IsActive)
            return new(DepartmentWriteStatus.Saved, View(department));

        db.Entry(department).Property(x => x.RowVersion).OriginalValue = version;
        department.Change(name, actor, DateTimeOffset.UtcNow);
        Audit(department, name is null ? "DepartmentArchived" : "DepartmentUpdated", traceId);
        try
        {
            await db.SaveChangesAsync(ct);
            return new(DepartmentWriteStatus.Saved, View(department));
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(DepartmentWriteStatus.Conflict);
        }
    }

    private void Audit(Department department, string action, string traceId) =>
        db.AuditEntries.Add(new AuditEntry(department.UpdatedByUserId, action, "Department",
            department.Id, department.UpdatedAtUtc, traceId,
            JsonSerializer.Serialize(new { department.Code, department.Name, department.IsActive })));

    private static DepartmentView View(Department x) => new(x.Id, x.Code, x.Name, x.IsActive, x.RowVersion);
}
