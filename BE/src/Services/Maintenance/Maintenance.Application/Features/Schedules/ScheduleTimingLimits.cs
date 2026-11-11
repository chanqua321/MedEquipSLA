namespace MedEquipSLA.Maintenance.Application.Features.Schedules;

public static class ScheduleTimingLimits
{
    public static bool IsUsable(ScheduleTiming timing, DateTimeOffset next)
    {
        if (timing.IntervalDays <= 0 || timing.ReminderLeadMinutes < 0 || timing.CompletionWindowMinutes < 0) return false;
        try
        {
            _ = next.AddMinutes(-timing.ReminderLeadMinutes);
            _ = next.AddMinutes(timing.CompletionWindowMinutes);
            _ = next.AddDays(timing.IntervalDays);
            return true;
        }
        catch (ArgumentOutOfRangeException) { return false; }
    }
}
