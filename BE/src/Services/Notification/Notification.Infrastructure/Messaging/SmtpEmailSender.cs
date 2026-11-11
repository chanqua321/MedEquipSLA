using System.Net;
using System.Net.Mail;
using System.Text;
using MedEquipSLA.Notification.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace MedEquipSLA.Notification.Infrastructure.Messaging;

public sealed class SmtpEmailSender(IConfiguration config) : IEmailSender
{
    public async Task SendAsync(OutgoingEmail message, CancellationToken ct)
    {
        var host = config["Email:Host"]; var from = config["Email:From"];
        var port = config.GetValue("Email:Port", 587);
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from) || port is < 1 or > 65535)
            throw new InvalidOperationException("SMTP host, port and sender must be configured.");
        using var mail = new MailMessage(from, message.Address, message.Subject, message.Body)
        { SubjectEncoding = Encoding.UTF8, BodyEncoding = Encoding.UTF8, IsBodyHtml = false };
        mail.Headers.Add("Message-ID", message.MessageId);
        using var client = new SmtpClient(host, port) { EnableSsl = config.GetValue("Email:EnableSsl", true), Timeout = 10000 };
        if (config["Email:Username"] is { Length: > 0 } username)
            client.Credentials = new NetworkCredential(username, config["Email:Password"]);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        await client.SendMailAsync(mail, deadline.Token);
    }
}
