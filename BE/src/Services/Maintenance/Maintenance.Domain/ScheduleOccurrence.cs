namespace MedEquipSLA.Maintenance.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class ScheduleOccurrence
{
    public void MarkWorkOrderCreated()
    {
        if (State != "Planned") throw new InvalidOperationException("Only a planned occurrence may generate work.");
        State = "WorkOrderCreated";
    }
    public void Cancel(DateTimeOffset now)
    {
        if (State != "Planned") throw new InvalidOperationException("Only a planned occurrence can be cancelled.");
        State = "Cancelled"; CancelledAtUtc = now.ToUniversalTime();
    }
    internal static ScheduleOccurrence Plan(Guid scheduleId, DateTimeOffset occurrence, int windowMinutes,
        int reminderLeadMinutes, DateTimeOffset now) => new()
    {
        ScheduleId = scheduleId,
        ScheduledOccurrenceAtUtc = occurrence,
        DueAtUtc = occurrence.AddMinutes(windowMinutes),
        ReminderWindowStartAtUtc = occurrence.AddMinutes(-reminderLeadMinutes),
        CreatedAtUtc = now,
        State = "Planned"
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ScheduleId { get; private set; }
    public DateTimeOffset ScheduledOccurrenceAtUtc { get; private set; }
    public DateTimeOffset DueAtUtc { get; private set; }
    public DateTimeOffset? ReminderWindowStartAtUtc { get; private set; }
    public string State { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
