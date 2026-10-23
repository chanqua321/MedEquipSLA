namespace MedEquipSLA.Incident.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class IncidentRecord
{
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
