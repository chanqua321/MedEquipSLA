namespace MedEquipSLA.Maintenance.Application.Features.Schedules;

public sealed record ScheduleSearch(string? Search, bool? Active, int Page);
public sealed record ScheduleRow(Guid Id, string Code, string Equipment, string Department, string WorkType,
    bool IsActive, int IntervalDays, int ReminderLeadMinutes, int CompletionWindowMinutes, DateTimeOffset NextOccurrenceAtUtc);
public sealed record OccurrenceRow(Guid Id, DateTimeOffset ScheduledOccurrenceAtUtc, DateTimeOffset DueAtUtc,
    string State, Guid? WorkOrderId);
public sealed record SchedulePage<T>(IReadOnlyList<T> Items, bool HasNext);

public interface IScheduleReader
{
    Task<SchedulePage<ScheduleRow>> ListAsync(ScheduleSearch search, CancellationToken ct);
    Task<SchedulePage<OccurrenceRow>?> OccurrencesAsync(Guid scheduleId, int page, CancellationToken ct);
    Task<ScheduleDetail?> FindAsync(Guid id, CancellationToken ct);
}
