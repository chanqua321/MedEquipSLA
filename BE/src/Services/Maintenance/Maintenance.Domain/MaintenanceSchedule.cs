namespace MedEquipSLA.Maintenance.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class MaintenanceSchedule
{
    public void End(Guid actor, string reason, DateTimeOffset now)
    {
        if (EndedAtUtc is not null || actor == Guid.Empty || reason.Trim().Length is < 1 or > 2000)
            throw new InvalidOperationException("A schedule ends once with an Admin reason.");
        IsActive = false; EndedAtUtc = now.ToUniversalTime(); EndedByUserId = actor; EndReason = reason.Trim();
        UpdatedAtUtc = now.ToUniversalTime(); UpdatedByUserId = actor; AggregateVersion++;
    }
    public void AuthorizeReleaseRetry(Guid actor, DateTimeOffset now)
    {
        if (EndedAtUtc is null || RegistrationReleasedAtUtc is not null) throw new InvalidOperationException("Unresolved ended schedule required.");
        UpdatedAtUtc = now.ToUniversalTime(); UpdatedByUserId = actor; AggregateVersion++;
    }
    public void RecordRegistrationRelease(DateTimeOffset now)
    {
        if (EndedAtUtc is null) throw new InvalidOperationException("Only permanently ended schedules release.");
        RegistrationReleasedAtUtc ??= now.ToUniversalTime();
    }
    public DateTimeOffset? EndedAtUtc { get; private set; }
    public Guid? EndedByUserId { get; private set; }
    public string? EndReason { get; private set; }
    public DateTimeOffset? RegistrationReleasedAtUtc { get; private set; }
    public static MaintenanceSchedule Create(Guid id, Guid equipment, string equipmentCode, string equipmentName,
        Guid department, string departmentName, Guid registration, string workType, DateTimeOffset anchor,
        int interval, int lead, int window, bool active, Guid actor, DateTimeOffset now) => new()
    {
        Id = id, Code = $"MNT-{id:N}", EquipmentId = equipment, EquipmentCodeSnapshot = equipmentCode,
        EquipmentNameSnapshot = equipmentName, DepartmentIdSnapshot = department, DepartmentNameSnapshot = departmentName,
        EquipmentUseRegistrationId = registration, WorkType = workType, AnchorAtUtc = anchor, NextOccurrenceAtUtc = anchor,
        IntervalDays = interval, ReminderLeadMinutes = lead, CompletionWindowMinutes = window, IsActive = active,
        CreatedAtUtc = now, UpdatedAtUtc = now, CreatedByUserId = actor, UpdatedByUserId = actor
    };

    public void Revise(DateTimeOffset anchor, DateTimeOffset next, int interval, int lead, int window,
        bool active, Guid actor, DateTimeOffset now)
    {
        if (EndedAtUtc is not null) throw new InvalidOperationException("Ended schedules cannot be edited or reactivated.");
        anchor = anchor.ToUniversalTime();
        next = next.ToUniversalTime();
        now = now.ToUniversalTime();
        if (!IsActive && active && next < now)
        {
            var period = checked((long)interval * TimeSpan.TicksPerDay);
            var elapsed = (now - anchor).Ticks;
            var periods = elapsed / period + (elapsed % period == 0 ? 0 : 1);
            var reactivated = anchor.AddTicks(checked(periods * period));
            if (reactivated > next) next = reactivated;
        }
        AnchorAtUtc = anchor;
        NextOccurrenceAtUtc = next;
        IntervalDays = interval;
        ReminderLeadMinutes = lead;
        CompletionWindowMinutes = window;
        IsActive = active;
        UpdatedAtUtc = now;
        UpdatedByUserId = actor;
        AggregateVersion++;
    }

    public ScheduleOccurrence PlanNext(DateTimeOffset now)
    {
        if (!IsActive || NextOccurrenceAtUtc > now.AddMinutes(ReminderLeadMinutes))
            throw new InvalidOperationException("This schedule is outside its planning window.");
        var occurrence = ScheduleOccurrence.Plan(Id, NextOccurrenceAtUtc, CompletionWindowMinutes, ReminderLeadMinutes, now);
        NextOccurrenceAtUtc = NextOccurrenceAtUtc.AddDays(IntervalDays);
        UpdatedAtUtc = now;
        AggregateVersion++;
        return occurrence;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = string.Empty;
    public Guid EquipmentId { get; private set; }
    public string EquipmentCodeSnapshot { get; private set; } = string.Empty;
    public string EquipmentNameSnapshot { get; private set; } = string.Empty;
    public Guid DepartmentIdSnapshot { get; private set; }
    public string DepartmentNameSnapshot { get; private set; } = string.Empty;
    public string WorkType { get; private set; } = string.Empty;
    public DateTimeOffset AnchorAtUtc { get; private set; }
    public int IntervalDays { get; private set; }
    public DateTimeOffset NextOccurrenceAtUtc { get; private set; }
    public int ReminderLeadMinutes { get; private set; }
    public int CompletionWindowMinutes { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid EquipmentUseRegistrationId { get; private set; }
    public long AggregateVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
