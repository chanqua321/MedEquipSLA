namespace MedEquipSLA.Incident.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class IncidentRecord
{
    public static IncidentRecord Open(Guid id, Guid equipmentId, string equipmentCode, string equipmentName,
        Guid departmentId, string departmentName, Guid registrationId, Guid actor, string title,
        string description, string priority, Guid? relatedIncidentId, SlaPolicyVersion policy, DateTimeOffset now) => new()
    {
        Id = id,
        Code = $"INC-{id:N}",
        EquipmentId = equipmentId,
        EquipmentCodeSnapshot = equipmentCode,
        EquipmentNameSnapshot = equipmentName,
        DepartmentIdSnapshot = departmentId,
        DepartmentNameSnapshot = departmentName,
        EquipmentUseRegistrationId = registrationId,
        ReportedByUserId = actor,
        Title = title,
        Description = description,
        Priority = priority,
        RelatedIncidentId = relatedIncidentId,
        Status = "Open",
        SlaPolicyId = policy.SlaPolicyId,
        SlaPolicyVersion = policy.Version,
        SlaPrioritySnapshot = priority,
        ResponseDurationMinutesSnapshot = policy.ResponseDurationMinutes,
        ResolutionDurationMinutesSnapshot = policy.ResolutionDurationMinutes,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
        CreatedByUserId = actor,
        UpdatedByUserId = actor,
        ResponseDeadlineAtUtc = now.AddMinutes(policy.ResponseDurationMinutes),
        ResolutionDeadlineAtUtc = now.AddMinutes(policy.ResolutionDurationMinutes)
    };

    public void Assign(Guid technician, Guid actor, DateTimeOffset now, long authorizationVersion = 0)
    {
        if (Status is not ("Open" or "InProgress")) throw new InvalidOperationException("Incident cannot be assigned.");
        AssignedTechnicianId = technician;
        AssignedAuthorizationVersion = authorizationVersion;
        Touch(actor, now);
    }
    public long AssignedAuthorizationVersion { get; private set; }
    public bool ClearIneligibleAssignment(Guid technician, long authorizationVersion, DateTimeOffset now)
    {
        if (AssignedTechnicianId != technician || AssignedAuthorizationVersion >= authorizationVersion || Status == "Closed") return false;
        AssignedTechnicianId = null; UpdatedAtUtc = now; AggregateVersion++; return true;
    }

    public void StartRepair(Guid actor, DateTimeOffset now)
    {
        if (Status != "Open" || AssignedTechnicianId != actor)
            throw new InvalidOperationException("Only the assigned technician can start an open incident.");
        Status = "InProgress";
        FirstRespondedAtUtc ??= now;
        Touch(actor, now);
    }

    public void ReturnForRepair(Guid actor, DateTimeOffset now)
    {
        if (Status != "Resolved") throw new InvalidOperationException("Only resolved incidents can be returned.");
        Status = "InProgress";
        Touch(actor, now);
    }

    public void SubmitRepair(Guid actor, DateTimeOffset now)
    {
        if (Status != "InProgress") throw new InvalidOperationException("Incident must be in progress.");
        Status = "Resolved";
        Touch(actor, now);
    }

    public void Close(ResolutionAttempt accepted, Guid actor, DateTimeOffset now)
    {
        if (Status != "Resolved" || accepted.IncidentId != Id || accepted.ReviewDecision != "Accepted")
            throw new InvalidOperationException("A resolved incident with an accepted attempt is required.");
        Status = "Closed";
        ClosedAtUtc = now;
        SlaFinalizedAtUtc = now;
        ResponseOutcome = FirstRespondedAtUtc is { } response && response <= ResponseDeadlineAtUtc ? "Met" : "Breached";
        ResolutionOutcome = accepted.CompletedAtUtc <= ResolutionDeadlineAtUtc ? "Met" : "Breached";
        Touch(actor, now);
    }

    private void Touch(Guid actor, DateTimeOffset now)
    {
        UpdatedByUserId = actor;
        UpdatedAtUtc = now;
        AggregateVersion++;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = string.Empty;
    public Guid EquipmentId { get; private set; }
    public string EquipmentCodeSnapshot { get; private set; } = string.Empty;
    public string EquipmentNameSnapshot { get; private set; } = string.Empty;
    public Guid DepartmentIdSnapshot { get; private set; }
    public string DepartmentNameSnapshot { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid ReportedByUserId { get; private set; }
    public Guid? AssignedTechnicianId { get; private set; }
    public Guid? RelatedIncidentId { get; private set; }
    public string Priority { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public Guid SlaPolicyId { get; private set; }
    public int SlaPolicyVersion { get; private set; }
    public string SlaPrioritySnapshot { get; private set; } = string.Empty;
    public int ResponseDurationMinutesSnapshot { get; private set; }
    public int ResolutionDurationMinutesSnapshot { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ResponseDeadlineAtUtc { get; private set; }
    public DateTimeOffset ResolutionDeadlineAtUtc { get; private set; }
    public DateTimeOffset? FirstRespondedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public string ResponseOutcome { get; private set; } = "Pending";
    public string ResolutionOutcome { get; private set; } = "Pending";
    public DateTimeOffset? SlaFinalizedAtUtc { get; private set; }
    public Guid EquipmentUseRegistrationId { get; private set; }
    public long AggregateVersion { get; private set; } = 1;
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
