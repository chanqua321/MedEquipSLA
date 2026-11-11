namespace MedEquipSLA.Maintenance.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class OccurrenceReminder
{
    public static OccurrenceReminder Trigger(Guid occurrence, string kind, Guid eventId, DateTimeOffset now) => new()
    {
        OccurrenceId = occurrence, ReminderKind = kind, EventId = eventId, TriggeredAtUtc = now
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OccurrenceId { get; private set; }
    public string ReminderKind { get; private set; } = string.Empty;
    public Guid EventId { get; private set; }
    public DateTimeOffset TriggeredAtUtc { get; private set; }
}
