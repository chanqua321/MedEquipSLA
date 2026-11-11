using System.Security.Claims;
using MedEquipSLA.Notification.Application.Abstractions;

namespace MedEquipSLA.Notification.Api;

internal static class PersonalNotificationEndpoints
{
    public static void MapPersonalNotifications(this WebApplication app)
    {
        var routes = app.MapGroup("/api/notifications").WithTags("Personal notifications").RequireAuthorization();
        routes.MapGet("", async (bool? unreadOnly, int? page, ClaimsPrincipal user,
            IPersonalNotificationQueries queries, CancellationToken ct) =>
        {
            if (!TryRecipient(user, out var recipient)) return Results.Forbid();
            if (page is < 1 or > 1_000_000) return Results.BadRequest();
            return Results.Ok(await queries.ListAsync(recipient, unreadOnly ?? false, page ?? 1, ct));
        });

        routes.MapPut("/{id:guid}/read", async (Guid id, ClaimsPrincipal user,
            IPersonalNotificationCommands commands, CancellationToken ct) =>
        {
            if (!TryRecipient(user, out var recipient)) return Results.Forbid();
            return await commands.MarkReadAsync(recipient, id, ct) ? Results.NoContent() : Results.NotFound();
        });
    }

    private static bool TryRecipient(ClaimsPrincipal user, out Guid recipient) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out recipient) && recipient != Guid.Empty;
}
