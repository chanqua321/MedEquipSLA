using System.Security.Claims;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Notification.Application.Abstractions;

namespace MedEquipSLA.Notification.Api;

internal static class NotificationSettingsEndpoints
{
    internal static void MapNotificationSettings(this WebApplication app)
    {
        var preferences = app.MapGroup("/api/notification-preferences").WithTags("Notification preferences").RequireAuthorization();
        preferences.MapGet("", async (HttpContext http, INotificationPreferences store, CancellationToken ct) =>
        {
            if (!Actor(http, out var user)) return Results.Forbid();
            return Results.Ok(new { kinds = UserNotificationEvent.Kinds, defaults = new { inAppEnabled = true, emailEnabled = true },
                overrides = await store.ListAsync(user, ct) });
        });
        preferences.MapPut("/{kind}", async (string kind, PreferenceInput input, HttpContext http, INotificationPreferences store, CancellationToken ct) =>
        {
            if (!Actor(http, out var user)) return Results.Forbid();
            if (!UserNotificationEvent.Kinds.Contains(kind) || input.RowVersion is { Length: not 8 }) return Results.BadRequest();
            return await store.SaveAsync(user, kind, input.InAppEnabled, input.EmailEnabled, input.RowVersion, ct) ? Results.NoContent() : Results.Conflict();
        });
        var deliveries = app.MapGroup("/api/email-deliveries").WithTags("Email delivery administration")
            .RequireAuthorization(p => p.RequireRole("Admin"));
        deliveries.MapGet("", async (string? status, int? page, IEmailDeliveryReader reader, CancellationToken ct) =>
        {
            if (page is < 1 or > 1000000 || status is not (null or "Pending" or "Processing" or "Sent" or "Failed")) return Results.BadRequest();
            return Results.Ok(await reader.ListAsync(status, page ?? 1, ct));
        });
        deliveries.MapGet("/{id:guid}/attempts", async (Guid id, IEmailDeliveryReader reader, CancellationToken ct) =>
            Results.Ok(await reader.AttemptsAsync(id, ct)));

        deliveries.MapPost("/{id:guid}/retry", async (Guid id, RetryInput input, HttpContext http, IEmailDeliveryQueue queue, CancellationToken ct) =>
        {
            if (!Actor(http, out var user)) return Results.Forbid();
            if (input.RowVersion is not { Length: 8 } || input.Reason?.Trim().Length is not (> 0 and <= 2000)) return Results.BadRequest();
            var result = await queue.RetryAsync(id, user, input.RowVersion, input.Reason.Trim(), ct);
            return result switch { "Saved" => Results.Accepted(), "NotFound" => Results.NotFound(), _ => Results.Conflict() };
        });
    }
    private static bool Actor(HttpContext http, out Guid actor) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out actor) && actor != Guid.Empty;
    internal sealed record PreferenceInput(bool InAppEnabled, bool EmailEnabled, byte[]? RowVersion);
    internal sealed record RetryInput(byte[]? RowVersion, string? Reason);
}
