using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using MedEquipSLA.Notification.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace MedEquipSLA.Notification.Infrastructure.Messaging;

public sealed class RecipientEmailDirectory(HttpClient client, IConfiguration config) : IRecipientEmailDirectory
{
    public async Task<SecurityEmail?> FindSecurityMailAsync(Guid user, Guid ticket, CancellationToken ct)
    {
        var key = config["Identity:RecipientLookupKey"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Recipient lookup credential missing.");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/security-mail/{user}/{ticket}");
        request.Headers.Add("X-Recipient-Lookup-Key", key);
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var mail = await response.Content.ReadFromJsonAsync<SecurityEmail>(cancellationToken: ct);
        if (mail?.UserId != user || mail.Address.Length > 256 || !MailAddress.TryCreate(mail.Address, out _) ||
            mail.Subject.Length > 200 || mail.Body.Length > 4000) throw new InvalidOperationException("Security mail binding mismatch.");
        return mail;
    }
    public async Task<string?> FindActiveEmailAsync(Guid user, CancellationToken ct)
    {
        var key = config["Identity:RecipientLookupKey"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Recipient lookup credential missing.");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/notification-email/{user}");
        request.Headers.Add("X-Recipient-Lookup-Key", key);
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Reply>(cancellationToken: ct);
        if (result?.Id != user) throw new InvalidOperationException("Recipient lookup binding mismatch.");
        return result.Email is { Length: > 0 and <= 256 } && MailAddress.TryCreate(result.Email, out _) ? result.Email : null;
    }
    private sealed record Reply(Guid Id, string? Email);
}
