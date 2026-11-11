namespace MedEquipSLA.Maintenance.Application.Features.Schedules;

public sealed record ScheduleTiming(DateTimeOffset AnchorAtUtc, int IntervalDays, int ReminderLeadMinutes,
    int CompletionWindowMinutes, bool IsActive);
public sealed record ScheduleDetail(Guid Id, string Code, Guid EquipmentId, string EquipmentCode, string EquipmentName,
    Guid DepartmentId, string DepartmentName, string WorkType, ScheduleTiming Timing, DateTimeOffset NextOccurrenceAtUtc,
    byte[] RowVersion, DateTimeOffset? EndedAtUtc = null, Guid? EndedByUserId = null, string? EndReason = null,
    DateTimeOffset? RegistrationReleasedAtUtc = null);
public enum ScheduleStatus { Saved, Conflict, NotFound, InvalidEquipment, Unavailable, InvalidTiming }
public sealed record ScheduleResult(ScheduleStatus Status, ScheduleDetail? Schedule = null, Guid? OperationId = null);

public interface IScheduleCommands
{
    Task<ScheduleResult> CreateAsync(Guid equipment, string workType, ScheduleTiming timing, Guid actor,
        Guid key, string authorization, CancellationToken ct);
    Task<ScheduleResult> UpdateAsync(Guid id, ScheduleTiming timing, byte[] version, Guid actor,
        string correlation, CancellationToken ct);
}
