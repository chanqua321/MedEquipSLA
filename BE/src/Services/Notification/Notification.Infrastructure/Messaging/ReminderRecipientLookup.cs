using System.Net.Http.Json;
using MedEquipSLA.Contracts.Events;
using Microsoft.Extensions.Configuration;

namespace MedEquipSLA.Notification.Infrastructure.Messaging;

public sealed class ReminderRecipientLookup(HttpClient client, IConfiguration config) : INotificationRecipientLookup
{
    public async Task<Guid[]> ResolveAsync(Guid? technicianId, CancellationToken ct)
    {
        var key = config["Identity:RecipientLookupKey"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Configure Identity recipient lookup credentials.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/notification-recipients");
        request.Headers.Add("X-Recipient-Lookup-Key", key);
        request.Content = JsonContent.Create(new NotificationRecipientRequest(technicianId));
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var reply = await response.Content.ReadFromJsonAsync<NotificationRecipientReply>(ct);
        return reply?.UserIds ?? throw new InvalidOperationException("Invalid recipient directory response.");
    }
}
