using System.Text.Json;
using MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence;

public sealed class UserResourceRegistrationStore(EquipmentDbContext db) : IUserResourceRegistrationStore
{
    public async Task<UserResourceRegistrationResult> RegisterAsync(Guid id, Guid operation, string source, string resourceType, Guid? department, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var asset = await db.EquipmentAssets.FromSqlInterpolated(
            $"SELECT * FROM dbo.EquipmentAssets WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleOrDefaultAsync(ct);
        if (asset is null || department is not null && asset.DepartmentId != department) return new(RegistrationOutcome.NotFound);
        if (asset.IsRetired) return new(RegistrationOutcome.Retired);
        var registration = await db.EquipmentUseRegistrations.SingleOrDefaultAsync(x => x.SourceService == source && x.SourceOperationId == operation, ct);
        if (registration is null)
        {
            if (await db.EquipmentRetirementIntents.AnyAsync(x => x.EquipmentId == id && x.State != "Cancelled" && x.State != "Completed", ct))
                return new(RegistrationOutcome.AdmissionClosed);
            var now = DateTimeOffset.UtcNow;
            registration = EquipmentUseRegistration.Register(id, source, resourceType, operation, operation, now);
            db.Add(registration);
            db.Add(AuditEntry.ForService(source, "ResourceUseRegistered", "EquipmentUseRegistration", registration.Id, now,
                operation.ToString(), JsonSerializer.Serialize(new { equipmentId = id, operation, resourceType })));
            await db.SaveChangesAsync(ct);
        }
        else if (registration.EquipmentId != id || registration.ResourceType != resourceType || registration.State is not ("Pending" or "Active"))
            return new(RegistrationOutcome.Terminal);
        var unit = await db.Departments.AsNoTracking().SingleAsync(x => x.Id == asset.DepartmentId, ct);
        await transaction.CommitAsync(ct);
        return new(RegistrationOutcome.Saved, new(registration.Id, asset.AssetCode, asset.Name, unit.Id, unit.Name));
    }
}
