namespace MedEquipSLA.Maintenance.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class WorkOrderAttempt
{
    public static WorkOrderAttempt Submit(Guid order, int number, Guid actor, string result, string notes, decimal? cost,
        string? currency, byte[] photo, string contentType, DateTimeOffset now)
    {
        if (result is not ("Passed" or "Failed") || notes.Trim().Length is < 1 or > 4000 || photo.Length is < 1 or > 5242880 ||
            contentType is not ("image/png" or "image/jpeg") || cost < 0 || cost >= 10_000_000_000_000_000m ||
            currency is not null && (currency.Length != 3 || currency.Any(c => c is < 'A' or > 'Z')) ||
            cost is not null && (currency is null || decimal.Round(cost.Value, 2) != cost.Value))
            throw new InvalidOperationException("A valid report, result and equipment photo are required.");
        return new() { WorkOrderId = order, AttemptNumber = number, PerformedByUserId = actor, ResultStatus = result,
            Notes = notes.Trim(), Cost = cost, CurrencyCode = currency, OperationPhoto = photo.ToArray(),
            OperationPhotoContentType = contentType, CompletedAtUtc = now.ToUniversalTime() };
    }
    public void Review(Guid actor, bool accept, string? reason, DateTimeOffset now)
    {
        if (ReviewDecision != "Pending" || now < CompletedAtUtc || accept && (ResultStatus != "Passed" || OperationPhoto is null) ||
            !accept && reason?.Trim().Length is not (> 0 and <= 2000))
            throw new InvalidOperationException("Only Passed may be accepted; rejection requires a reason.");
        ReviewDecision = accept ? "Accepted" : "Rejected"; ReviewedByUserId = actor; ReviewedAtUtc = now.ToUniversalTime();
        ReviewReason = reason?.Trim();
    }
    public byte[]? OperationPhoto { get; private set; }
    public string? OperationPhotoContentType { get; private set; }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkOrderId { get; private set; }
    public int AttemptNumber { get; private set; }
    public Guid PerformedByUserId { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public string ResultStatus { get; private set; } = string.Empty;
    public string Notes { get; private set; } = string.Empty;
    public decimal? Cost { get; private set; }
    public string? CurrencyCode { get; private set; }
    public string ReviewDecision { get; private set; } = "Pending";
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public string? ReviewReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
