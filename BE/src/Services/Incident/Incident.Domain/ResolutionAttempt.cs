namespace MedEquipSLA.Incident.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class ResolutionAttempt
{
    public static ResolutionAttempt Submit(Guid incident, int number, Guid actor, string notes, string result,
        DateTimeOffset completed, byte[] photo, string contentType) => new()
    {
        IncidentId = incident, AttemptNumber = number, PerformedByUserId = actor,
        ResolutionNotes = notes, ResultSummary = result, CompletedAtUtc = completed,
        OperationPhoto = photo, OperationPhotoContentType = contentType
    };

    public void Accept(Guid actor, DateTimeOffset now)
    {
        if (ReviewDecision != "Pending" || OperationPhoto is not { Length: > 0 } || now < CompletedAtUtc)
            throw new InvalidOperationException("Only a pending attempt with an operation photo can be accepted.");
        ReviewDecision = "Accepted";
        ReviewedByUserId = actor;
        ReviewedAtUtc = now;
    }

    public void Reject(Guid actor, string reason, DateTimeOffset now)
    {
        if (ReviewDecision != "Pending" || string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A pending attempt and a rejection reason are required.");
        ReviewDecision = "Rejected";
        ReviewedByUserId = actor;
        ReviewReason = reason;
        ReviewedAtUtc = now;
    }

    public byte[]? OperationPhoto { get; private set; }
    public string? OperationPhotoContentType { get; private set; }

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
