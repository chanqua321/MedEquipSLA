using System.Linq.Expressions;
using MedEquipSLA.Maintenance.Application.Features.WorkOrders;
using MedEquipSLA.Maintenance.Domain;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;

public sealed class WorkOrderReader(MaintenanceDbContext db) : IWorkOrderReader
{
    public async Task<WorkOrderPhoto?> PhotoAsync(Guid id, Guid attempt, Guid actor, bool admin, CancellationToken ct)
    {
        if (!await CanReadEvidenceAsync(id, actor, admin, ct)) return null;
        var row = await db.WorkOrderAttempts.AsNoTracking().Where(x => x.WorkOrderId == id && x.Id == attempt)
            .Select(x => new { x.OperationPhoto, x.OperationPhotoContentType }).SingleOrDefaultAsync(ct);
        return row?.OperationPhoto is { } bytes && row.OperationPhotoContentType is { } type ? new(bytes, type) : null;
    }
    public async Task<IReadOnlyList<CalibrationMeasurementRow>?> MeasurementsAsync(Guid id, Guid attempt, Guid actor, bool admin, CancellationToken ct)
    {
        if (!await CanReadEvidenceAsync(id, actor, admin, ct)) return null;
        return await (from measurement in db.CalibrationMeasurements.AsNoTracking()
            join report in db.WorkOrderAttempts on measurement.WorkOrderAttemptId equals report.Id
            where report.WorkOrderId == id && report.Id == attempt orderby measurement.ParameterCode
            select new CalibrationMeasurementRow(measurement.ParameterCode, measurement.ParameterName, measurement.Unit,
                measurement.MeasuredValue, measurement.MinimumAllowed, measurement.MaximumAllowed, measurement.Passed)).ToArrayAsync(ct);
    }
    private Task<bool> CanReadEvidenceAsync(Guid id, Guid actor, bool admin, CancellationToken ct) =>
        db.WorkOrders.AnyAsync(x => x.Id == id && (admin || x.AssignedTechnicianId == actor), ct);
    public async Task<WorkOrderPage> ListAsync(WorkOrderSearch search, CancellationToken ct)
    {
        const int pageSize = 20;
        var orders = db.WorkOrders.AsNoTracking();
        if (!string.IsNullOrEmpty(search.Search))
            orders = orders.Where(x => x.Code.Contains(search.Search) || x.EquipmentNameSnapshot.Contains(search.Search));
        if (search.Status is not null)
            orders = orders.Where(x => x.Status == search.Status);

        var rows = await orders.OrderBy(x => x.ScheduledForUtc).ThenBy(x => x.Id)
            .Skip((search.Page - 1) * pageSize).Take(pageSize + 1)
            .Select(ToRow).ToListAsync(ct);
        return new(rows.Take(pageSize).ToArray(), rows.Count > pageSize);
    }

    private static readonly Expression<Func<WorkOrder, WorkOrderRow>> ToRow = x =>
        new(x.Id, x.Code, x.EquipmentNameSnapshot, x.DepartmentNameSnapshot, x.WorkType, x.Status,
            x.ScheduledForUtc, x.DueAtUtc, x.IsBlocking, x.AssignedTechnicianId, x.RowVersion, x.EquipmentId);

    public Task<WorkOrderRow?> FindAsync(Guid id, CancellationToken ct) =>
        db.WorkOrders.AsNoTracking().Where(x => x.Id == id).Select(ToRow).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<WorkOrderAttemptRow>?> ReadAttemptsAsync(Guid id, CancellationToken ct)
    {
        if (!await db.WorkOrders.AnyAsync(x => x.Id == id, ct)) return null;
        return await db.WorkOrderAttempts.AsNoTracking().Where(x => x.WorkOrderId == id)
            .OrderByDescending(x => x.AttemptNumber)
            .Select(x => new WorkOrderAttemptRow(x.AttemptNumber, x.ResultStatus, x.Notes, x.CompletedAtUtc,
                x.ReviewDecision, x.ReviewReason, x.Id, x.OperationPhotoContentType != null)).ToListAsync(ct);
    }
}
