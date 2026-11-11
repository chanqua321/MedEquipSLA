using MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence;

public sealed class ReconciliationReader(EquipmentDbContext db) : IReconciliationReader
{
    public async Task<IReadOnlyList<ReconciliationView>> ListAsync(CancellationToken ct) =>
        await db.ReconciliationCheckpoints.AsNoTracking().OrderBy(x=>x.SourceService)
            .Select(x=>new ReconciliationView(x.SourceService,x.SnapshotId,x.SnapshotWatermark,x.Cursor,x.State,
                x.StartedAtUtc,x.LastCompletedAtUtc,x.LastError)).ToArrayAsync(ct);
}
