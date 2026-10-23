namespace MedEquipSLA.Maintenance.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class WorkOrderAttempt
{
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
