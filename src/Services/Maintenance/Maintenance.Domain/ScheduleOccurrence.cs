namespace MedEquipSLA.Maintenance.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class ScheduleOccurrence
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ScheduleId { get; private set; }
    public DateTimeOffset ScheduledOccurrenceAtUtc { get; private set; }
    public DateTimeOffset DueAtUtc { get; private set; }
    public string State { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
