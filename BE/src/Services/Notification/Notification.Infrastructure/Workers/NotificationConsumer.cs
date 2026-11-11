using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Notification.Domain;
using MedEquipSLA.Notification.Infrastructure.Persistence;
using MedEquipSLA.Notification.Infrastructure.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace MedEquipSLA.Notification.Infrastructure.Workers;

public sealed class NotificationConsumer(IConfiguration config, IServiceScopeFactory scopes, ILogger<NotificationConsumer> logger)
    : BackgroundService
{
    private const string Group = "notification-sla-v1";
    public const string ReminderGroup = "notification-maintenance-reminders-v1";
    public const string UserGroup = "notification-users-v1";
    public const string IdentityMailGroup = "notification-identity-mail-v1";
    public const string UserAccessGroup = "notification-user-access-v1";
    public const string SessionGroup = "notification-user-sessions-v1";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue<bool>("Workers:Enabled")) return;
        var consumer = Guid.NewGuid().ToString("N");
        RedisValue slaCursor = "0-0";
        RedisValue reminderCursor = "0-0";
        RedisValue userCursor = "0-0";
        RedisValue identityCursor = "0-0";
        RedisValue accessCursor = "0-0";
        RedisValue sessionCursor = "0-0";
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        do
        {
            try { slaCursor = await PollAsync(SlaAlertEvent.Stream, consumer, slaCursor, stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogWarning(exception, "SLA stream cycle failed; pending messages will retry."); }
            try { reminderCursor = await PollAsync(MaintenanceReminderEvent.Stream, consumer, reminderCursor, stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogWarning(exception, "Maintenance reminder stream cycle failed; pending messages will retry."); }
            try { userCursor = await PollAsync(UserNotificationEvent.Stream, consumer, userCursor, stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogWarning(exception, "User notification stream cycle failed; pending messages will retry."); }
            try { identityCursor = await PollAsync(IdentityMailEvent.Stream, consumer, identityCursor, stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogWarning(exception, "Identity mail stream cycle failed; pending messages will retry."); }
            try { accessCursor = await PollAsync(UserAccessChangedEvent.Stream, consumer, accessCursor, stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogWarning(exception, "User access stream cycle failed; pending messages will retry."); }
            try { sessionCursor = await PollAsync(SessionRevokedEvent.Stream, consumer, sessionCursor, stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogWarning(exception, "Session revocation stream cycle failed; pending messages will retry."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<RedisValue> PollAsync(string stream, string consumer, RedisValue cursor, CancellationToken ct)
    {
        var group = GroupFor(stream);
        await using var scope = scopes.CreateAsyncScope();
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        try { await redis.StreamCreateConsumerGroupAsync(stream, group, "0-0", createStream: true); }
        catch (RedisServerException exception) when (exception.Message.StartsWith("BUSYGROUP", StringComparison.Ordinal)) { }

        var idle = Math.Clamp(config.GetValue("Workers:ConsumerClaimIdleMilliseconds", 30_000), 100, 300_000);
        var claimed = await redis.StreamAutoClaimAsync(stream, group, consumer, idle, cursor, count: 10);
        var fresh = await redis.StreamReadGroupAsync(stream, group, consumer, ">", count: 10);
        foreach (var entry in claimed.ClaimedEntries.Concat(fresh))
        {
            ct.ThrowIfCancellationRequested();
            await ProcessAsync(redis, entry, stream, ct);
        }
        var pending = await redis.StreamPendingAsync(stream, group);
        logger.LogInformation("Consumed stream {Stream}; fresh {FreshCount}; reclaimed {ReclaimedCount}; pending {PendingCount}.",
            stream, fresh.Length, claimed.ClaimedEntries.Length, pending.PendingMessageCount);
        return claimed.NextStartId;
    }

    public async Task ProcessAsync(IDatabase redis, StreamEntry entry, string stream, CancellationToken ct)
    {
        var group = GroupFor(stream);
        var raw = entry.Values.FirstOrDefault(x => x.Name == "event").Value;
        IntegrationEvent? message = null;
        MaintenanceReminderEvent? reminder = null;
        SlaAlertEvent? sla = null;
        UserNotificationEvent? userEvent = null;
        IdentityMailEvent? securityMail = null;
        UserAccessChangedEvent? access = null;
        SessionRevokedEvent? session = null;
        try
        {
            message = raw.IsNull ? null : JsonSerializer.Deserialize<IntegrationEvent>(raw.ToString());
            if (message is null || message.EventId == Guid.Empty || message.AggregateId == Guid.Empty ||
                message.SchemaVersion != 1 || message.AggregateVersion < 1 || string.IsNullOrWhiteSpace(message.Payload))
                throw new JsonException("Invalid event envelope.");
            if (stream == SessionRevokedEvent.Stream)
            {
                session = JsonSerializer.Deserialize<SessionRevokedEvent>(message.Payload);
                if (message.Producer != "Identity" || message.EventType != SessionRevokedEvent.Revoked || session is null ||
                    session.UserId != message.AggregateId || session.SessionId == Guid.Empty || session.Reason is not ("Logout" or "RefreshReuse"))
                    throw new JsonException("Invalid session revocation event.");
            }
            else if (stream == UserAccessChangedEvent.Stream)
            {
                access = JsonSerializer.Deserialize<UserAccessChangedEvent>(message.Payload);
                if (message.Producer != "Identity" || message.EventType != UserAccessChangedEvent.Changed || access is null ||
                    access.UserId != message.AggregateId || access.AuthorizationVersion != message.AggregateVersion)
                    throw new JsonException("Invalid user access event.");
            }
            else if (stream == IdentityMailEvent.Stream)
            {
                securityMail = JsonSerializer.Deserialize<IdentityMailEvent>(message.Payload);
                if (message.Producer != "Identity" || message.EventType != IdentityMailEvent.Requested || securityMail is null ||
                    securityMail.UserId == Guid.Empty || securityMail.TicketId != message.AggregateId)
                    throw new JsonException("Invalid Identity security mail event.");
            }
            else if (stream == MaintenanceReminderEvent.Stream)
            {
                reminder = JsonSerializer.Deserialize<MaintenanceReminderEvent>(message.Payload);
                if (!ValidReminder(message, reminder)) throw new JsonException("Invalid maintenance reminder.");
            }
            else if (stream == UserNotificationEvent.Stream)
            {
                userEvent = JsonSerializer.Deserialize<UserNotificationEvent>(message.Payload);
                if (message.EventType != UserNotificationEvent.Requested || userEvent is null ||
                    userEvent.ResourceId != message.AggregateId || userEvent.Title is not { Length: > 0 and <= 200 } ||
                    userEvent.Body is not { Length: > 0 and <= 4000 } ||
                    !(message.Producer == "Incident" && userEvent.ResourceType == "Incident" &&
                        userEvent.Kind is "IncidentCreated" or "IncidentAssigned" or "IncidentResolved" or "IncidentClosed" ||
                      message.Producer == "Maintenance" && userEvent.ResourceType == "WorkOrder" &&
                        userEvent.Kind is "WorkOrderAssigned" or "WorkOrderCompleted") ||
                    !(userEvent.Audience == "Admins" && userEvent.RecipientUserId is null ||
                      userEvent.Audience == "Recipient" && userEvent.RecipientUserId is { } recipient && recipient != Guid.Empty))
                    throw new JsonException("Invalid user notification request.");
            }
            else
            {
                sla = JsonSerializer.Deserialize<SlaAlertEvent>(message.Payload);
                if (sla is null || sla.RecipientUserId == Guid.Empty || message.Producer != "Incident" ||
                    message.EventType is not (SlaAlertEvent.ResponseBreached or SlaAlertEvent.ResolutionBreached))
                    throw new JsonException("Invalid SLA event.");
            }
        }
        catch (JsonException)
        {
            await MoveToDeadLetterAsync(redis, stream, group, entry.Id, raw, "InvalidEnvelope");
            return;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            if (await db.InboxMessages.AnyAsync(x => x.ConsumerName == group && x.EventId == message!.EventId, ct))
                logger.LogInformation("Duplicate event {EventId} already committed to Inbox {ConsumerGroup}.", message!.EventId, group);
            else if (session is not null)
            {
                db.Add(new InboxMessage(SessionGroup, message!.EventId, message.EventType, 1, DateTimeOffset.UtcNow));
                await db.SaveChangesAsync(ct);
            }
            else if (access is not null)
            {
                db.Add(new InboxMessage(UserAccessGroup, message!.EventId, message.EventType, 1, DateTimeOffset.UtcNow));
                await db.SaveChangesAsync(ct);
            }
            else if (securityMail is not null)
            {
                var rendered = await scope.ServiceProvider.GetRequiredService<MedEquipSLA.Notification.Application.Abstractions.IRecipientEmailDirectory>()
                    .FindSecurityMailAsync(securityMail.UserId, securityMail.TicketId, ct);
                await StoreSecurityMailAsync(db, message!, securityMail, rendered, ct);
            }
            else if (reminder is not null)
            {
                var lookup = scope.ServiceProvider.GetRequiredService<INotificationRecipientLookup>();
                var active = await lookup.ResolveAsync(reminder.AssignedTechnicianId, ct);
                var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
                if (reminder.ReminderKind == "DueSoon" && now >= reminder.ScheduledOccurrenceAtUtc)
                    logger.LogInformation("Expired DueSoon event {EventId}; record Inbox without delivering a stale notification.", message!.EventId);
                await StoreReminderAsync(db, message!, reminder, active, now, ct);
            }
            else if (userEvent is not null)
            {
                var recipients = userEvent.Audience == "Admins"
                    ? await scope.ServiceProvider.GetRequiredService<INotificationRecipientLookup>().ResolveAsync(null, ct)
                    : new[] { userEvent.RecipientUserId!.Value };
                await StoreUserEventAsync(db, message!, userEvent, recipients,
                    scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(), ct);
            }
            else await StoreAsync(db, message!, sla!, ct);
            // Replay this effect even for an existing Inbox row (crash after SQL commit before disconnect/ACK).
            if (access is not null) scope.ServiceProvider.GetRequiredService<Realtime.NotificationConnections>()
                .DisconnectOlder(access.UserId, access.AuthorizationVersion);
            if (session is not null) scope.ServiceProvider.GetRequiredService<Realtime.NotificationConnections>()
                .DisconnectSession(session.UserId, session.SessionId);
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            var pending = await redis.StreamPendingMessagesAsync(stream, group, 1,
                consumerName: RedisValue.Null, minId: entry.Id, maxId: entry.Id);
            if (pending is [{ DeliveryCount: >= 10 }])
                await MoveToDeadLetterAsync(redis, stream, group, entry.Id, raw, "DeliveryLimit");
            else logger.LogWarning(exception, "Event {EventId} remains pending in {ConsumerGroup}.", message!.EventId, group);
            return;
        }

        // Durable Inbox and Notifications are committed before acknowledgement.
        await redis.StreamAcknowledgeAsync(stream, group, entry.Id);
        logger.LogInformation("Acknowledged event {EventId} in {ConsumerGroup}.", message!.EventId, group);
    }

    private static bool ValidReminder(IntegrationEvent message, MaintenanceReminderEvent? payload)
    {
        if (message.Producer != "Maintenance" || payload is null || payload.ScheduleId == Guid.Empty ||
            payload.EquipmentId == Guid.Empty || payload.DepartmentId == Guid.Empty ||
            payload.EquipmentCode is not { Length: > 0 and <= 50 } || payload.EquipmentName is not { Length: > 0 and <= 200 } ||
            payload.RecipientUserIds is not { Length: > 0 and <= 1000 } || payload.RecipientUserIds.Any(x => x == Guid.Empty) ||
            payload.DueAtUtc < payload.ScheduledOccurrenceAtUtc || payload.AssignedTechnicianId == Guid.Empty)
            return false;
        return message.EventType switch
        {
            MaintenanceReminderEvent.DueSoon => payload.ReminderKind == "DueSoon" &&
                payload.ReminderWindowStartAtUtc is { } start && start <= message.OccurredAtUtc &&
                message.OccurredAtUtc < payload.ScheduledOccurrenceAtUtc,
            MaintenanceReminderEvent.Overdue => payload.ReminderKind == "Overdue" && message.OccurredAtUtc >= payload.DueAtUtc,
            _ => false
        };
    }

    public static async Task StoreAsync(NotificationDbContext db, IntegrationEvent message, SlaAlertEvent payload, CancellationToken ct)
    {
        var processed = db.InboxMessages.Where(x => x.ConsumerName == Group && x.EventId == message.EventId);
        if (await processed.AnyAsync(ct))
            return;

        var now = DateTimeOffset.UtcNow;
        var preference = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == payload.RecipientUserId &&
            x.NotificationKind == message.EventType, ct);
        if (preference is null || preference.InAppEnabled || preference.EmailEnabled)
        {
            var deadline = payload.DeadlineAtUtc.ToString("O");
            var notification = new NotificationRecord(payload.RecipientUserId, message.EventId,
                message.EventType, message.AggregateId, payload.DepartmentId, deadline, now);
            notification.SetChannels(preference?.InAppEnabled ?? true, preference?.EmailEnabled ?? true);
            db.Notifications.Add(notification);
        }
        db.InboxMessages.Add(new InboxMessage(Group, message.EventId, message.EventType, message.SchemaVersion, now));
        try
        {
            // EF commits the Inbox marker and notification in a single SQL transaction.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            if (!await processed.AnyAsync(ct))
                throw;
        }
    }


    public static async Task StoreReminderAsync(NotificationDbContext db, IntegrationEvent message,
        MaintenanceReminderEvent payload, Guid[] activeRecipients, DateTimeOffset now, CancellationToken ct)
    {
        var processed = db.InboxMessages.Where(x => x.ConsumerName == ReminderGroup && x.EventId == message.EventId);
        if (await processed.AnyAsync(ct)) return;
        var expired = payload.ReminderKind == "DueSoon" && now >= payload.ScheduledOccurrenceAtUtc;
        var ids = expired ? Array.Empty<Guid>() : payload.RecipientUserIds.Intersect(activeRecipients).ToArray();
        var preferences = await db.NotificationPreferences.AsNoTracking().Where(x => ids.Contains(x.UserId) &&
            x.NotificationKind == message.EventType).ToArrayAsync(ct);
        foreach (var recipient in ids)
        {
            var preference = preferences.SingleOrDefault(x => x.UserId == recipient);
            if (preference is { InAppEnabled: false, EmailEnabled: false }) continue;
            var notification = NotificationRecord.Reminder(recipient, message.EventId, message.EventType,
                message.AggregateId, payload.EquipmentCode, payload.EquipmentName, payload.DepartmentId,
                payload.ReminderKind, payload.ScheduledOccurrenceAtUtc, payload.DueAtUtc, now);
            notification.SetChannels(preference?.InAppEnabled ?? true, preference?.EmailEnabled ?? true); db.Add(notification);
        }
        db.InboxMessages.Add(new InboxMessage(ReminderGroup, message.EventId, message.EventType, message.SchemaVersion, now));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            if (!await processed.AnyAsync(ct)) throw;
        }
    }

    private static string GroupFor(string stream) => stream switch
    {
        SessionRevokedEvent.Stream => SessionGroup,
        UserAccessChangedEvent.Stream => UserAccessGroup,
        IdentityMailEvent.Stream => IdentityMailGroup,
        SlaAlertEvent.Stream => Group,
        MaintenanceReminderEvent.Stream => ReminderGroup,
        UserNotificationEvent.Stream => UserGroup,
        _ => throw new ArgumentException("Unsupported notification stream.", nameof(stream))
    };

    public static async Task StoreSecurityMailAsync(NotificationDbContext db, IntegrationEvent message, IdentityMailEvent payload,
        MedEquipSLA.Notification.Application.Abstractions.SecurityEmail? mail, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var resource = "IdentityMail:" + message.EventId.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51071,'Identity mail lock timeout',1", ct);
        if (await db.InboxMessages.AnyAsync(x => x.ConsumerName == IdentityMailGroup && x.EventId == message.EventId, ct)) return;
        var now = DateTimeOffset.UtcNow;
        if (mail is not null)
        {
            // Persist only a reference. Fetch the still-valid secret just before SMTP; never persist the plaintext link/OTP.
            var notification = NotificationRecord.UserEvent(payload.UserId, message.EventId, "AccountSecurity", "IdentityTicket",
                payload.TicketId, null, "MedEquipSLA account security", "Security content is rendered by Identity at send time.", now);
            notification.SetChannels(false, true);
            db.Add(notification); db.Add(EmailDelivery.Queue(notification, mail.Address, now));
        }
        db.Add(new InboxMessage(IdentityMailGroup, message.EventId, message.EventType, 1, now));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public static async Task StoreUserEventAsync(NotificationDbContext db, IntegrationEvent message, UserNotificationEvent payload,
        Guid[] recipients, DateTimeOffset now, CancellationToken ct)
    {
        if (await db.InboxMessages.AnyAsync(x => x.ConsumerName == UserGroup && x.EventId == message.EventId, ct)) return;
        var ids = recipients.Where(x => x != Guid.Empty).Distinct().Take(1000).ToArray();
        var preferences = await db.NotificationPreferences.AsNoTracking().Where(x => ids.Contains(x.UserId) &&
            x.NotificationKind == payload.Kind).ToArrayAsync(ct);
        foreach (var id in ids)
        {
            var preference = preferences.SingleOrDefault(x => x.UserId == id);
            if (preference is { InAppEnabled: false, EmailEnabled: false }) continue;
            var notification = NotificationRecord.UserEvent(id, message.EventId, payload.Kind, payload.ResourceType,
                payload.ResourceId, payload.DepartmentId, payload.Title, payload.Body, now);
            notification.SetChannels(preference?.InAppEnabled ?? true, preference?.EmailEnabled ?? true);
            db.Add(notification);
        }
        db.Add(new InboxMessage(UserGroup, message.EventId, message.EventType, 1, now));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        {
            if (!await db.InboxMessages.AnyAsync(x => x.ConsumerName == UserGroup && x.EventId == message.EventId, ct)) throw;
        }
    }

    private async Task MoveToDeadLetterAsync(IDatabase redis, string stream, string group, RedisValue id, RedisValue raw, string reason)
    {
        await redis.StreamAddAsync(stream + ":dead-letter",
            [new("sourceId", id), new("reason", reason), new("event", raw.IsNull ? "" : raw)]);
        await redis.StreamAcknowledgeAsync(stream, group, id);
        logger.LogWarning("Moved {Stream} entry {StreamId} to dead letter. Reason={Reason}", stream, id, reason);
    }
}
