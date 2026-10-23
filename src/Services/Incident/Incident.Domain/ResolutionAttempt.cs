namespace MedEquipSLA.Incident.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class ResolutionAttempt
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid IncidentId { get; private set; }
    public int AttemptNumber { get; private set; }
    public Guid PerformedByUserId { get; private set; }
    public string ResolutionNotes { get; private set; } = string.Empty;
    public string ResultSummary { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public string ReviewDecision { get; private set; } = "Pending";
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public string? ReviewReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
