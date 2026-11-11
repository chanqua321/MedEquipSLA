using System.Net.Mail;
using System.Text.Json;
using MedEquipSLA.Notification.Application.Abstractions;
using MedEquipSLA.Notification.Domain;
using MedEquipSLA.Notification.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Notification.Infrastructure.Persistence;

public sealed class EmailQueueCursor { public Guid? LastId { get; set; } }
public sealed class EmailDeliveryQueue(NotificationDbContext db, IRecipientEmailDirectory directory, IEmailSender sender,
    TimeProvider time, EmailQueueCursor cursor) : IEmailDeliveryQueue
{
    public async Task<int> QueueBatchAsync(CancellationToken ct)
    {
        var query = db.Notifications.AsNoTracking().Where(x => x.IsEmailRequested && !db.EmailDeliveries.Any(e => e.NotificationId == x.Id));
        if (cursor.LastId is { } last) query = query.Where(x => x.Id.CompareTo(last) > 0);
        var candidates = await query.OrderBy(x => x.Id).Select(x => new { x.Id, x.RecipientUserId }).Take(50).ToArrayAsync(ct);
        cursor.LastId = candidates.Length == 0 ? null : candidates[^1].Id;
        var total = 0;
        foreach (var candidate in candidates)
        {
            // Identity is read before the local SQL transaction. Failure leaves this notification eligible.
            var address = await directory.FindActiveEmailAsync(candidate.RecipientUserId, ct);
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var notification = await db.Notifications.FromSqlInterpolated(
                $"SELECT * FROM dbo.Notifications WITH (UPDLOCK,HOLDLOCK) WHERE Id={candidate.Id}").SingleAsync(ct);
            if (!notification.IsEmailRequested || await db.EmailDeliveries.AnyAsync(x => x.NotificationId == notification.Id, ct)) continue;
            var preference = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x =>
                x.UserId == notification.RecipientUserId && x.NotificationKind == notification.Kind, ct);
            var delivery = EmailDelivery.Queue(notification, address, time.GetUtcNow());
            if (preference is { EmailEnabled: false }) delivery.Suppress("PreferenceDisabled");
            db.Add(delivery);
            db.Add(AuditEntry.Record("EmailDelivery", delivery.Id, "EmailQueued", time.GetUtcNow(),
                JsonSerializer.Serialize(new { notification.Id, delivery.Status })));
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); total++;
        }
        return total;
    }

    public async Task<int> SendBatchAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var candidates = await db.EmailDeliveries.AsNoTracking().Where(x =>
            x.Status == "Pending" && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now) ||
            x.Status == "Processing" && x.LeaseUntilUtc <= now)
            .OrderBy(x => x.NextAttemptAtUtc).ThenBy(x => x.Id).Select(x => x.Id).Take(20).ToArrayAsync(ct);
        var sent = 0;
        foreach (var id in candidates)
        {
            var header = await (from email in db.EmailDeliveries.AsNoTracking()
                join notice in db.Notifications on email.NotificationId equals notice.Id
                where email.Id == id select new { notice.RecipientUserId, notice.Kind, notice.ResourceType, notice.ResourceId }).SingleAsync(ct);
            var lease = Guid.NewGuid().ToString("N");
            Guid attemptId; OutgoingEmail message; bool optedOut;
            db.ChangeTracker.Clear();
            await using (var claim = await db.Database.BeginTransactionAsync(ct))
            {
                var delivery = await LockedAsync(id, ct);
                now = time.GetUtcNow();
                if (delivery.Status is not ("Pending" or "Processing") || delivery.LeaseUntilUtc > now ||
                    delivery.NextAttemptAtUtc > now) continue;
                var unresolved = await db.EmailDeliveryAttempts.Where(x => x.EmailDeliveryId == id && x.Outcome == "Processing").ToArrayAsync(ct);
                foreach (var previous in unresolved) previous.Finish("Unknown", "WorkerLeaseExpired", now);
                if (delivery.AttemptCount >= 8)
                {
                    delivery.Exhaust("RetryLimitUnknown");
                    db.Add(AuditEntry.Record("EmailDelivery", id, "EmailRetryLimit", now, "{}"));
                    await db.SaveChangesAsync(ct); await claim.CommitAsync(ct); continue;
                }
                var number = (await db.EmailDeliveryAttempts.Where(x => x.EmailDeliveryId == id).Select(x => (int?)x.AttemptNumber).MaxAsync(ct) ?? 0) + 1;
                delivery.Claim(lease, now);
                var attempt = EmailDeliveryAttempt.Start(id, number, now); db.Add(attempt); attemptId = attempt.Id;
                optedOut = header.ResourceType != "IdentityTicket" && await db.NotificationPreferences.AnyAsync(x => x.UserId == header.RecipientUserId &&
                    x.NotificationKind == header.Kind && !x.EmailEnabled, ct);
                message = new(delivery.ToAddressSnapshot, delivery.Subject, delivery.Body, delivery.MessageId);
                db.Add(AuditEntry.Record("EmailDelivery", id, "EmailAttemptStarted", now, JsonSerializer.Serialize(new { attemptId, number })));
                await db.SaveChangesAsync(ct); await claim.CommitAsync(ct);
            }
            bool success = false, permanent = false; string? error = null;
            try
            {
                var security = header.ResourceType == "IdentityTicket" ? await directory.FindSecurityMailAsync(header.RecipientUserId, header.ResourceId, ct) : null;
                var currentAddress = header.ResourceType == "IdentityTicket" ? security?.Address : await directory.FindActiveEmailAsync(header.RecipientUserId, ct);
                if (optedOut || currentAddress is null || !string.Equals(currentAddress, message.Address, StringComparison.OrdinalIgnoreCase))
                { permanent = true; error = optedOut ? "PreferenceDisabled" : "RecipientUnavailableOrChanged"; }
                else { await sender.SendAsync(security is null ? message : message with { Subject = security.Subject, Body = security.Body }, ct); success = true; }
            }
            catch (Exception failure) when (!ct.IsCancellationRequested)
            {
                permanent = failure is ArgumentException or InvalidOperationException ||
                    failure is SmtpException smtp && (int)smtp.StatusCode >= 500;
                error = failure is SmtpException mail ? $"SMTP:{(int)mail.StatusCode}" :
                    failure is OperationCanceledException ? "TimeoutUnknown" : failure.GetType().Name;
            }
            db.ChangeTracker.Clear();
            await using var final = await db.Database.BeginTransactionAsync(ct);
            var current = await LockedAsync(id, ct);
            var result = await db.EmailDeliveryAttempts.SingleAsync(x => x.Id == attemptId, ct);
            if (current.Owned(lease))
            {
                current.Finish(success, permanent, error, time.GetUtcNow());
                result.Finish(success ? "Sent" : "Failed", error, time.GetUtcNow());
                if (success) sent++;
            }
            else result.Finish("Unknown", "StaleLeaseResult", time.GetUtcNow());
            db.Add(AuditEntry.Record("EmailDelivery", id, success && current.Status == "Sent" ? "EmailSent" : "EmailAttemptResult",
                time.GetUtcNow(), JsonSerializer.Serialize(new { attemptId, error, status = current.Status })));
            await db.SaveChangesAsync(ct); await final.CommitAsync(ct);
        }
        return sent;
    }

    public async Task<string> RetryAsync(Guid id, Guid actor, byte[] rowVersion, string reason, CancellationToken ct)
    {
        if (rowVersion.Length != 8 || actor == Guid.Empty || reason.Trim().Length is < 1 or > 2000) return "Conflict";
        var recipient = await (from email in db.EmailDeliveries.AsNoTracking() join notice in db.Notifications on email.NotificationId equals notice.Id
            where email.Id == id select new { notice.RecipientUserId, notice.Kind, notice.ResourceType, notice.ResourceId }).SingleOrDefaultAsync(ct);
        if (recipient is null) return "NotFound";
        var address = recipient.ResourceType == "IdentityTicket"
            ? (await directory.FindSecurityMailAsync(recipient.RecipientUserId, recipient.ResourceId, ct))?.Address
            : await directory.FindActiveEmailAsync(recipient.RecipientUserId, ct);
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var delivery = await LockedAsync(id, ct);
        if (!rowVersion.SequenceEqual(delivery.RowVersion) || delivery.Status != "Failed" || address is null ||
            recipient.ResourceType != "IdentityTicket" && await db.NotificationPreferences.AnyAsync(x => x.UserId == recipient.RecipientUserId && x.NotificationKind == recipient.Kind && !x.EmailEnabled, ct))
            return "Conflict";
        delivery.Retry(address, time.GetUtcNow());
        db.Add(AuditEntry.Record("EmailDelivery", id, "EmailRetryRequested", time.GetUtcNow(), JsonSerializer.Serialize(new { reason }), actor));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return "Saved";
    }
    private Task<EmailDelivery> LockedAsync(Guid id, CancellationToken ct) => db.EmailDeliveries.FromSqlInterpolated(
        $"SELECT * FROM dbo.EmailDeliveries WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleAsync(ct);
}
