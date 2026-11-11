using System.Security.Cryptography;
using System.Text;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Identity.Application.Abstractions;

namespace MedEquipSLA.Identity.Api;

internal static class NotificationRecipientEndpoints
{
    public static void MapNotificationRecipients(this WebApplication app)
    {
        app.MapGet("/api/internal/technician-access/{id:guid}", async (Guid id, HttpContext http, IConfiguration config,
            IIdentityRecipientDirectory directory, CancellationToken ct) =>
        {
            var key = config["Identity:RecipientLookupKey"]; var supplied = http.Request.Headers["X-Recipient-Lookup-Key"];
            if (string.IsNullOrWhiteSpace(key) || supplied.Count != 1 || string.IsNullOrEmpty(supplied[0]) ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(supplied[0]!))) return Results.Unauthorized();
            return Results.Ok(await directory.TechnicianAsync(id, ct));
        }).AllowAnonymous().WithTags("Internal technician access");
        app.MapPost("/api/internal/notification-recipients", async (NotificationRecipientRequest input,
            HttpContext http, IConfiguration config, IIdentityRecipientDirectory directory, CancellationToken ct) =>
        {
            var key = config["Identity:RecipientLookupKey"];
            var supplied = http.Request.Headers["X-Recipient-Lookup-Key"].ToString();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(supplied) ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(supplied)))
                return Results.Unauthorized();

            var ids = await directory.RecipientsAsync(input.TechnicianId, ct);
            return Results.Ok(new NotificationRecipientReply(ids));
        }).AllowAnonymous().WithTags("Internal recipient lookup");
        app.MapGet("/api/internal/notification-email/{id:guid}", async (Guid id, HttpContext http,
            IConfiguration config, IIdentityRecipientDirectory directory, CancellationToken ct) =>
        {
            var key = config["Identity:RecipientLookupKey"];
            var supplied = http.Request.Headers["X-Recipient-Lookup-Key"];
            if (string.IsNullOrWhiteSpace(key) || supplied.Count != 1 || string.IsNullOrEmpty(supplied[0]) ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(supplied[0]!)))
                return Results.Unauthorized();
            return await directory.EmailAsync(id, ct) is { } user ? Results.Ok(user) : Results.NotFound();
        }).AllowAnonymous().WithTags("Internal recipient lookup");
    }
}
