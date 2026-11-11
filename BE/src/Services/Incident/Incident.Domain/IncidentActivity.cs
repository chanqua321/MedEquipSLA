namespace MedEquipSLA.Incident.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class IncidentActivity
{
    public static IncidentActivity Record(Guid incident, string action, Guid actor, DateTimeOffset now,
        string fromStatus, string toStatus, Guid? fromTechnician, Guid? toTechnician, string? reason = null) => new()
    {
        IncidentId = incident, Action = action, ActorUserId = actor, OccurredAtUtc = now,
        FromStatus = fromStatus, ToStatus = toStatus, FromTechnicianId = fromTechnician,
        ToTechnicianId = toTechnician, Reason = reason
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid IncidentId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? FromStatus { get; private set; }
    public string? ToStatus { get; private set; }
    public Guid? FromTechnicianId { get; private set; }
    public Guid? ToTechnicianId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
}
