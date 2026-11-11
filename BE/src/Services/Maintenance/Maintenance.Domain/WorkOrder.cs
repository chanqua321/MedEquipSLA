namespace MedEquipSLA.Maintenance.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class WorkOrder
{
    public void RecordProgress(Guid actor, DateTimeOffset now)
    {
        if (Status != "InProgress" || AssignedTechnicianId != actor) throw new InvalidOperationException("Assigned technician required for progress.");
        Touch(actor, now);
    }
    public long AssignedAuthorizationVersion { get; private set; }
    public bool ClearIneligibleAssignment(Guid technician, long authorizationVersion, DateTimeOffset now)
    {
        if (AssignedTechnicianId != technician || AssignedAuthorizationVersion >= authorizationVersion || Status is "Closed" or "Cancelled") return false;
        AssignedTechnicianId = null; UpdatedAtUtc = now; AggregateVersion++; return true;
    }
    public void Start(Guid actor, DateTimeOffset now)
    {
        if (Status != "Scheduled" || AssignedTechnicianId != actor) throw new InvalidOperationException("Assigned technician required.");
        Status = "InProgress"; StartedAtUtc = now; IsBlocking = true; Touch(actor, now);
    }
    public void Complete(Guid actor, DateTimeOffset now)
    {
        if (Status != "InProgress" || AssignedTechnicianId != actor) throw new InvalidOperationException("Assigned technician required.");
        Status = "Completed"; IsBlocking = true; Touch(actor, now);
    }
    public void Review(WorkOrderAttempt attempt, Guid actor, bool accept, string? reason, DateTimeOffset now)
    {
        if (Status != "Completed" || attempt.WorkOrderId != Id) throw new InvalidOperationException("Completed work and its attempt required.");
        attempt.Review(actor, accept, reason, now);
        Status = accept ? "Closed" : "InProgress"; IsBlocking = !accept;
        if (accept) ClosedAtUtc = now;
        Touch(actor, now);
    }
    public void Cancel(Guid actor, string reason, DateTimeOffset now)
    {
        if (Status != "Scheduled" || StartedAtUtc is not null || reason.Trim().Length is < 1 or > 2000)
            throw new InvalidOperationException("Only work that has not started can be cancelled.");
        Status = "Cancelled"; CancelledAtUtc = now; CancellationReason = reason.Trim(); IsBlocking = false; Touch(actor, now);
    }
    private void Touch(Guid actor, DateTimeOffset now)
    { UpdatedAtUtc = now.ToUniversalTime(); UpdatedByUserId = actor; AggregateVersion++; }
    public static WorkOrder Schedule(WorkOrderRegistrationPayload payload, Guid registration, string equipmentCode,
        string equipmentName, Guid department, string departmentName, DateTimeOffset now)
    {
        _ = payload.CanonicalJson();
        if (registration == Guid.Empty || department == Guid.Empty) throw new InvalidOperationException("Registration snapshot is required.");
        return new()
        {
            Id = payload.WorkOrderId, Code = $"WO-{payload.WorkOrderId:N}", OccurrenceId = payload.OccurrenceId,
            EquipmentId = payload.EquipmentId, EquipmentCodeSnapshot = equipmentCode, EquipmentNameSnapshot = equipmentName,
            DepartmentIdSnapshot = department, DepartmentNameSnapshot = departmentName, WorkType = payload.WorkType,
            Status = "Scheduled", ScheduledForUtc = payload.ScheduledForUtc, DueAtUtc = payload.DueAtUtc,
            EquipmentUseRegistrationId = registration, IsBlocking = false, CreatedAtUtc = now, UpdatedAtUtc = now
        };
    }
    public void Assign(Guid technician, Guid actor, DateTimeOffset now, long authorizationVersion = 0)
    {
        if (Status is not ("Scheduled" or "InProgress"))
            throw new InvalidOperationException("This work order cannot be assigned.");
        AssignedTechnicianId = technician;
        AssignedAuthorizationVersion = authorizationVersion;
        UpdatedByUserId = actor;
        UpdatedAtUtc = now;
        AggregateVersion++;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = string.Empty;
    public Guid? OccurrenceId { get; private set; }
    public Guid EquipmentId { get; private set; }
    public string EquipmentCodeSnapshot { get; private set; } = string.Empty;
    public string EquipmentNameSnapshot { get; private set; } = string.Empty;
    public Guid DepartmentIdSnapshot { get; private set; }
    public string DepartmentNameSnapshot { get; private set; } = string.Empty;
    public string WorkType { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public Guid? AssignedTechnicianId { get; private set; }
    public DateTimeOffset ScheduledForUtc { get; private set; }
    public DateTimeOffset DueAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public bool IsBlocking { get; private set; }
    public Guid EquipmentUseRegistrationId { get; private set; }
    public long AggregateVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
