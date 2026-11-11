using MedEquipSLA.Notification.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Notification.Infrastructure.Persistence;

public sealed class EmailDeliveryReader(NotificationDbContext db) : IEmailDeliveryReader
{
    public async Task<IReadOnlyList<EmailDeliveryView>> ListAsync(string? status, int page, CancellationToken ct) =>
        await db.EmailDeliveries.AsNoTracking().Where(x => status == null || x.Status == status)
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * 50).Take(50)
            .Select(x => new EmailDeliveryView(x.Id, x.NotificationId, x.ToAddressSnapshot, x.Subject, x.Status, x.AttemptCount,
                x.NextAttemptAtUtc, x.SentAtUtc, x.LastError, x.RowVersion)).ToArrayAsync(ct);
    public async Task<IReadOnlyList<EmailAttemptView>> AttemptsAsync(Guid id, CancellationToken ct) =>
        await db.EmailDeliveryAttempts.AsNoTracking().Where(x => x.EmailDeliveryId == id).OrderBy(x => x.AttemptNumber)
            .Select(x => new EmailAttemptView(x.AttemptNumber, x.StartedAtUtc, x.FinishedAtUtc, x.Outcome, x.SmtpResponse, x.ErrorCode)).ToArrayAsync(ct);
}
