namespace MedEquipSLA.Incident.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class SlaDeadlineAlert
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid IncidentId { get; private set; }
    public string AlertKind { get; private set; } = string.Empty;
    public DateTimeOffset DeadlineAtUtc { get; private set; }
    public DateTimeOffset DetectedAtUtc { get; private set; }
    public Guid EventId { get; private set; }
}
