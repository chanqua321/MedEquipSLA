using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MedEquipSLA.Notification.Application.Abstractions;
using MedEquipSLA.Notification.Domain;
using MedEquipSLA.Notification.Infrastructure.Messaging;
using MedEquipSLA.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class EmailDeliveryTests
{
    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Directory : IRecipientEmailDirectory
    {
        internal string? Address = "recipient@tests.invalid";
        public Task<string?> FindActiveEmailAsync(Guid user, CancellationToken ct) => Task.FromResult(Address);
    }
    private static NotificationDbContext Db(TestDatabase catalog) => (NotificationDbContext)Contexts.Create("Notification", catalog.ConnectionString);
    private static IEmailSender Sender(SmtpCapture smtp) => new SmtpEmailSender(new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string,string?> { ["Email:Host"] = "127.0.0.1", ["Email:Port"] = smtp.Port.ToString(),
            ["Email:From"] = "sender@tests.invalid", ["Email:EnableSsl"] = "false" }).Build());
    private static async Task SeedAsync(TestDatabase catalog)
    {
        await using var db = Db(catalog); await db.Database.MigrateAsync();
        db.Add(NotificationRecord.UserEvent(Guid.NewGuid(), Guid.NewGuid(), "WorkOrderAssigned", "WorkOrder", Guid.NewGuid(), null,
            "Assigned", "Review your assigned work order.", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }
    private static EmailDeliveryQueue Queue(NotificationDbContext db, Directory directory, SmtpCapture smtp, Clock clock) =>
        new(db, directory, Sender(smtp), clock, new());

    [SqlServerFact]
    public async Task Real_smtp_ack_precedes_sent_state_and_duplicate_queue_does_not_create_second_delivery()
    {
        await using var catalog = await TestDatabase.CreateAsync("Notification");
        await SeedAsync(catalog);
        var clock = new Clock(); var directory = new Directory(); var observedProcessing = false;
        await using var smtp = new SmtpCapture(async () =>
        {
            await using var check = Db(catalog);
            Assert.Equal("Processing", (await check.EmailDeliveries.SingleAsync()).Status);
            Assert.Equal("Processing", (await check.EmailDeliveryAttempts.SingleAsync()).Outcome);
            observedProcessing = true;
        });
        await using var db = Db(catalog);
        var queue = Queue(db, directory, smtp, clock);
        Assert.Equal(1, await queue.QueueBatchAsync(default));
        Assert.Equal(0, await queue.QueueBatchAsync(default));
        Assert.Equal(1, await queue.SendBatchAsync(default));
        Assert.True(observedProcessing);
        db.ChangeTracker.Clear();
        var delivery = await db.EmailDeliveries.SingleAsync();
        Assert.Equal("Sent", delivery.Status); Assert.NotNull(delivery.SentAtUtc);
        Assert.Equal("Sent", (await db.EmailDeliveryAttempts.SingleAsync()).Outcome);
        Assert.Single(smtp.Messages);
        Assert.Contains(delivery.MessageId, smtp.Messages.Single(), StringComparison.Ordinal);
        Assert.Equal(0, await queue.SendBatchAsync(default));
    }

    [SqlServerFact]
    public async Task Temporary_smtp_failure_retries_after_restart_with_stable_message_id()
    {
        await using var catalog = await TestDatabase.CreateAsync("Notification"); await SeedAsync(catalog);
        var clock = new Clock(); var directory = new Directory();
        await using var smtp = new SmtpCapture { RecipientCode = 451 };
        string messageId;
        await using (var db = Db(catalog))
        {
            var queue = Queue(db, directory, smtp, clock); await queue.QueueBatchAsync(default);
            Assert.Equal(0, await queue.SendBatchAsync(default));
            db.ChangeTracker.Clear();
            var delivery = await db.EmailDeliveries.SingleAsync();
            Assert.Equal("Pending", delivery.Status); Assert.Null(delivery.SentAtUtc); Assert.NotNull(delivery.NextAttemptAtUtc);
            messageId = delivery.MessageId;
        }
        smtp.RecipientCode = 250; clock.Now = clock.Now.AddMinutes(2);
        await using var restart = Db(catalog);
        Assert.Equal(1, await Queue(restart, directory, smtp, clock).SendBatchAsync(default));
        Assert.Equal(new[] { "Failed", "Sent" }, await restart.EmailDeliveryAttempts.OrderBy(x => x.AttemptNumber).Select(x => x.Outcome).ToArrayAsync());
        Assert.Contains(messageId, smtp.Messages.Single(), StringComparison.Ordinal);
    }

    [SqlServerFact]
    public async Task Permanent_smtp_failure_is_failed_and_admin_retry_requires_current_rowversion()
    {
        await using var catalog = await TestDatabase.CreateAsync("Notification"); await SeedAsync(catalog);
        var clock = new Clock(); var directory = new Directory();
        await using var smtp = new SmtpCapture { RecipientCode = 550 };
        await using var db = Db(catalog);
        var queue = Queue(db, directory, smtp, clock); await queue.QueueBatchAsync(default);
        Assert.Equal(0, await queue.SendBatchAsync(default)); db.ChangeTracker.Clear();
        var delivery = await db.EmailDeliveries.SingleAsync();
        Assert.Equal("Failed", delivery.Status); Assert.Null(delivery.SentAtUtc);
        Assert.Equal("Conflict", await queue.RetryAsync(delivery.Id, Guid.NewGuid(), new byte[8], "Retry", default));
        Assert.Equal("Saved", await queue.RetryAsync(delivery.Id, Guid.NewGuid(), delivery.RowVersion, "Recipient repaired", default));
        smtp.RecipientCode = 250;
        Assert.Equal(1, await queue.SendBatchAsync(default));
        Assert.Equal(2, await db.EmailDeliveryAttempts.CountAsync());
    }

    [SqlServerFact]
    public async Task Smtp_success_with_local_commit_failure_remains_unknown_and_reclaim_preserves_history()
    {
        await using var catalog = await TestDatabase.CreateAsync("Notification"); await SeedAsync(catalog);
        var clock = new Clock(); var directory = new Directory(); await using var smtp = new SmtpCapture();
        await using (var db = Db(catalog))
        {
            var queue = Queue(db, directory, smtp, clock); await queue.QueueBatchAsync(default);
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.EmailDeliveries ADD CONSTRAINT CK_Test_SentFailure CHECK(Status<>N'Sent')");
            await Assert.ThrowsAsync<DbUpdateException>(() => queue.SendBatchAsync(default));
        }
        clock.Now = clock.Now.AddMinutes(2);
        await using var restart = Db(catalog);
        Assert.Equal("Processing", (await restart.EmailDeliveries.AsNoTracking().SingleAsync()).Status);
        await restart.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.EmailDeliveries DROP CONSTRAINT CK_Test_SentFailure");
        Assert.Equal(1, await Queue(restart, directory, smtp, clock).SendBatchAsync(default));
        Assert.Equal(new[] { "Unknown", "Sent" }, await restart.EmailDeliveryAttempts.OrderBy(x => x.AttemptNumber).Select(x => x.Outcome).ToArrayAsync());
        // SMTP is at-least-once: losing the SQL commit after acceptance can send the same Message-ID again.
        Assert.Equal(2, smtp.Messages.Count);
        Assert.Single(smtp.Messages.Select(x => x.Split("\r\n").Single(line => line.StartsWith("Message-ID:", StringComparison.OrdinalIgnoreCase))).Distinct());
    }

    internal sealed class SmtpCapture : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly Task loop;
        private readonly Func<Task>? beforeAck;
        internal int RecipientCode = 250;
        internal int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        internal ConcurrentQueue<string> Messages { get; } = new();
        internal SmtpCapture(Func<Task>? beforeAck = null)
        { this.beforeAck = beforeAck; listener.Start(); loop = AcceptAsync(); }
        private async Task AcceptAsync()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    using var client = await listener.AcceptTcpClientAsync(stop.Token);
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\r\n" };
                    await writer.WriteLineAsync("220 localhost test SMTP");
                    while (!stop.IsCancellationRequested && await reader.ReadLineAsync(stop.Token) is { } line)
                    {
                        if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase)) await writer.WriteLineAsync("250-localhost\r\n250 SIZE 6000000");
                        else if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase)) await writer.WriteLineAsync("250 Sender accepted");
                        else if (line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase)) await writer.WriteLineAsync($"{RecipientCode} Recipient response");
                        else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                        {
                            await writer.WriteLineAsync("354 Continue");
                            var data = new StringBuilder();
                            while (await reader.ReadLineAsync(stop.Token) is { } row && row != ".") data.Append(row).Append("\r\n");
                            Messages.Enqueue(data.ToString());
                            if (beforeAck is not null) await beforeAck();
                            await writer.WriteLineAsync("250 Accepted");
                        }
                        else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase)) { await writer.WriteLineAsync("221 Bye"); break; }
                        else await writer.WriteLineAsync("250 OK");
                    }
                }
            }
            catch (Exception error) when (stop.IsCancellationRequested && error is OperationCanceledException or SocketException or ObjectDisposedException) { }
        }
        public async ValueTask DisposeAsync()
        { stop.Cancel(); listener.Stop(); await loop; stop.Dispose(); }
    }
}
