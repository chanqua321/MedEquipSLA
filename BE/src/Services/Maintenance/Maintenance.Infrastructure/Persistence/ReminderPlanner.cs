using System.Data;
using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Maintenance.Domain;
using MedEquipSLA.Maintenance.Infrastructure.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;

public sealed class ReminderPlanner(MaintenanceDbContext db, INotificationRecipientLookup recipients,
    ILogger<ReminderPlanner> logger)
{
    public async Task<int> ScanAsync(DateTimeOffset now, CancellationToken ct)
    {
        now = now.ToUniversalTime();
        var candidates = await (from occurrence in db.ScheduleOccurrences.AsNoTracking()
                                join schedule in db.MaintenanceSchedules on occurrence.ScheduleId equals schedule.Id
                                where schedule.IsActive && occurrence.State != "Cancelled" &&
                                  (now >= occurrence.DueAtUtc &&
                                    !db.OccurrenceReminders.Any(x => x.OccurrenceId == occurrence.Id && x.ReminderKind == "Overdue") ||
                                   now < occurrence.ScheduledOccurrenceAtUtc && occurrence.ReminderWindowStartAtUtc <= now &&
                                    !db.OccurrenceReminders.Any(x => x.OccurrenceId == occurrence.Id && x.ReminderKind == "DueSoon")) &&
                                  !db.WorkOrders.Any(x => x.OccurrenceId == occurrence.Id && (x.Status == "Closed" || x.Status == "Cancelled"))
                                orderby occurrence.ScheduledOccurrenceAtUtc, occurrence.Id
                                select new { occurrence.Id, occurrence.ScheduleId }).Take(50).ToListAsync(ct);
        var legacy = await (from occurrence in db.ScheduleOccurrences
                            join schedule in db.MaintenanceSchedules on occurrence.ScheduleId equals schedule.Id
                            where schedule.IsActive && occurrence.State != "Cancelled" &&
                                occurrence.ReminderWindowStartAtUtc == null && now < occurrence.ScheduledOccurrenceAtUtc
                            select occurrence.Id).CountAsync(ct);
        if (legacy != 0) logger.LogWarning("Skipped DueSoon for {OccurrenceCount} legacy occurrences without a reminder window snapshot.", legacy);

        var created = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                var assigned = await db.WorkOrders.AsNoTracking().Where(x => x.OccurrenceId == candidate.Id)
                    .Select(x => x.AssignedTechnicianId).SingleOrDefaultAsync(ct);
                var users = (await recipients.ResolveAsync(assigned, ct)).Where(x => x != Guid.Empty).Distinct().ToArray();
                if (users.Length == 0)
                {
                    logger.LogWarning("No active recipients for occurrence {OccurrenceId}; retry on next scan.", candidate.Id);
                    continue;
                }

                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
                // Use the same schedule row as edits/planning to serialize an Inactive transition.
                var schedule = await db.MaintenanceSchedules.FromSqlInterpolated(
                    $"SELECT * FROM dbo.MaintenanceSchedules WITH (UPDLOCK,HOLDLOCK) WHERE Id={candidate.ScheduleId}").SingleAsync(ct);
                var occurrence = await db.ScheduleOccurrences.FromSqlInterpolated(
                    $"SELECT * FROM dbo.ScheduleOccurrences WITH (UPDLOCK,HOLDLOCK) WHERE Id={candidate.Id}").SingleAsync(ct);
                var work = await db.WorkOrders.AsNoTracking().SingleOrDefaultAsync(x => x.OccurrenceId == occurrence.Id, ct);
                if (!schedule.IsActive || occurrence.State == "Cancelled" || work?.AssignedTechnicianId != assigned ||
                    work?.Status is "Closed" or "Cancelled") continue;
                var kind = Kind(occurrence, now);
                if (kind is null) continue;
                if (await db.OccurrenceReminders.AnyAsync(x => x.OccurrenceId == occurrence.Id && x.ReminderKind == kind, ct))
                {
                    logger.LogInformation("Reminder {ReminderKind} already recorded for occurrence {OccurrenceId}.", kind, occurrence.Id);
                    continue;
                }

                var eventId = Guid.NewGuid();
                var type = kind == "DueSoon" ? MaintenanceReminderEvent.DueSoon : MaintenanceReminderEvent.Overdue;
                var payload = new MaintenanceReminderEvent(schedule.Id, schedule.EquipmentId, schedule.EquipmentCodeSnapshot,
                    schedule.EquipmentNameSnapshot, schedule.DepartmentIdSnapshot, occurrence.ScheduledOccurrenceAtUtc,
                    occurrence.DueAtUtc, occurrence.ReminderWindowStartAtUtc, kind, assigned, users);
                db.OccurrenceReminders.Add(OccurrenceReminder.Trigger(occurrence.Id, kind, eventId, now));
                db.OutboxMessages.Add(new OutboxMessage(new IntegrationEvent(eventId, type, 1, "Maintenance",
                    occurrence.Id, 1, now, eventId.ToString(), JsonSerializer.Serialize(payload)), "ScheduleOccurrence"));
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                created++;
                logger.LogInformation("Recorded {ReminderKind} for occurrence {OccurrenceId}; event {EventId}; recipients {RecipientCount}.",
                    kind, occurrence.Id, eventId, users.Length);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                logger.LogInformation(exception, "Reminder concurrency conflict for {OccurrenceId}; retry later.", candidate.Id);
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 or 1205 })
            {
                logger.LogInformation(exception, "Concurrent reminder write for {OccurrenceId}; retry later.", candidate.Id);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Reminder scan failed for {OccurrenceId}; retained for retry.", candidate.Id);
            }
            finally { db.ChangeTracker.Clear(); }
        }
        logger.LogInformation("Reminder scan selected {CandidateCount} occurrences and created {ReminderCount} reminders.",
            candidates.Count, created);
        return created;
    }

    private static string? Kind(ScheduleOccurrence occurrence, DateTimeOffset now)
    {
        if (now >= occurrence.DueAtUtc) return "Overdue";
        if (now < occurrence.ScheduledOccurrenceAtUtc && occurrence.ReminderWindowStartAtUtc <= now) return "DueSoon";
        return null;
    }
}
