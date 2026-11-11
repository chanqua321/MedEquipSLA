using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;

public sealed class OccurrencePlanner(MaintenanceDbContext db, OccurrencePlanningCursor cursor,
    IOptions<OccurrencePlanningOptions> options, ILogger<OccurrencePlanner> logger)
{
    public async Task<int> PlanBatchAsync(DateTimeOffset now, CancellationToken ct)
    {
        now = now.ToUniversalTime();
        var started = Stopwatch.GetTimestamp();
        var limits = options.Value;
        logger.LogInformation("Occurrence planning tick started at {TickAtUtc}.", now);
        var eligible = db.MaintenanceSchedules.AsNoTracking()
            .Where(x => x.IsActive && x.NextOccurrenceAtUtc.AddMinutes(-x.ReminderLeadMinutes) <= now);
        var after = cursor.Read();
        var candidates = eligible;
        if (after is { } last)
            candidates = candidates.Where(x => x.Id.CompareTo(last) > 0);
        var schedules = await candidates.OrderBy(x => x.Id).Select(x => x.Id)
            .Take(limits.ScheduleBatchSize).ToListAsync(ct);
        if (schedules.Count == 0 && after is not null)
            schedules = await eligible.OrderBy(x => x.Id).Select(x => x.Id)
                .Take(limits.ScheduleBatchSize).ToListAsync(ct);
        cursor.Advance(schedules.Count == 0 ? null : schedules[^1]);
        logger.LogInformation("Selected {ScheduleCount} schedules. Limit {ScheduleBatchSize}; per-schedule limit {OccurrencesPerSchedule}.",
            schedules.Count, limits.ScheduleBatchSize, limits.OccurrencesPerSchedule);

        var planned = 0;
        foreach (var id in schedules)
        {
            for (var attempt = 0; attempt < limits.OccurrencesPerSchedule; attempt++)
            {
                try
                {
                    var schedule = await db.MaintenanceSchedules.SingleOrDefaultAsync(x => x.Id == id, ct);
                    if (schedule is null) break;
                    if (!schedule.IsActive)
                    {
                        logger.LogInformation("Skipped inactive schedule {ScheduleId}.", id);
                        break;
                    }
                    if (schedule.NextOccurrenceAtUtc.AddMinutes(-schedule.ReminderLeadMinutes) > now) break;
                    var occurrence = schedule.PlanNext(now);
                    var exists = await db.ScheduleOccurrences.AnyAsync(x => x.ScheduleId == id &&
                        x.ScheduledOccurrenceAtUtc == occurrence.ScheduledOccurrenceAtUtc, ct);
                    if (!exists) db.ScheduleOccurrences.Add(occurrence);
                    // EF atomically commits the new occurrence and cursor update in one SQL transaction.
                    await db.SaveChangesAsync(ct);
                    if (!exists)
                    {
                        planned++;
                        logger.LogInformation("Planned occurrence {OccurrenceId} for schedule {ScheduleId} at {OccurrenceAtUtc}; lateness {LatenessSeconds} seconds.",
                            occurrence.Id, id, occurrence.ScheduledOccurrenceAtUtc,
                            Math.Max(0, (now - occurrence.ScheduledOccurrenceAtUtc).TotalSeconds));
                    }
                    else
                        logger.LogWarning("Recovered existing occurrence for schedule {ScheduleId} at {OccurrenceAtUtc}.",
                            id, occurrence.ScheduledOccurrenceAtUtc);
                }
                catch (DbUpdateConcurrencyException exception)
                {
                    logger.LogInformation(exception, "Concurrent planning or schedule edit for {ScheduleId}; retry on a later tick.", id);
                    break;
                }
                catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 or 1205 })
                {
                    logger.LogInformation(exception, "Concurrent SQL write for schedule {ScheduleId}; retry on a later tick.", id);
                    break;
                }
                catch (Exception exception) when (!ct.IsCancellationRequested)
                {
                    logger.LogError(exception, "Planning failed for schedule {ScheduleId}; other schedules will continue.", id);
                    break;
                }
                finally { db.ChangeTracker.Clear(); }
            }
        }
        var remaining = await eligible.CountAsync(ct);
        var oldest = await eligible.MinAsync(x => (DateTimeOffset?)x.NextOccurrenceAtUtc, ct);
        logger.LogInformation("Occurrence planning tick completed. Selected {ScheduleCount}; created {OccurrenceCount}; remaining {BacklogScheduleCount}; oldest due lateness {OldestLatenessSeconds} seconds; elapsed {ElapsedMilliseconds} ms.",
            schedules.Count, planned, remaining, oldest is { } date ? Math.Max(0, (now - date).TotalSeconds) : 0,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return planned;
    }
}
