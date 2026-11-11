using MedEquipSLA.Application.Coordination;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;

public static class EquipmentGateAccess
{
    public static async Task<EquipmentRetirementGate> LockAsync(MaintenanceDbContext db, Guid equipment, CancellationToken ct,
        DateTimeOffset? now = null)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Source gate requires a local SQL transaction.");
        var resource = $"EquipmentRetirementGate:{equipment:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result=sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
            IF @result<0 THROW 51030,'Source equipment admission lock could not be acquired.',1;
            """, ct);
        var gate = await db.EquipmentRetirementGates.SingleOrDefaultAsync(x => x.EquipmentId == equipment, ct);
        if (gate is not null)
        {
            // A scoped DbContext can span the remote RPC; never reuse its pre-freeze tracked values.
            await db.Entry(gate).ReloadAsync(ct);
            return gate;
        }
        gate = EquipmentRetirementGate.Open(equipment, now ?? DateTimeOffset.UtcNow);
        db.Add(gate);
        await db.SaveChangesAsync(ct);
        return gate;
    }
}
