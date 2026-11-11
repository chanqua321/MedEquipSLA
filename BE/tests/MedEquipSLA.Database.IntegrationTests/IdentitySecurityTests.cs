using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Identity.Application.Abstractions;
using MedEquipSLA.Identity.Infrastructure;
using MedEquipSLA.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using MedEquipSLA.Notification.Application.Abstractions;
using MedEquipSLA.Notification.Infrastructure.Persistence;
using MedEquipSLA.Notification.Infrastructure.Messaging;
using MedEquipSLA.Notification.Infrastructure.Workers;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.SqlClient;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class IdentitySecurityTests
{
    private sealed class SecurityDirectory(ServiceProvider source) : IRecipientEmailDirectory
    {
        public Task<string?> FindActiveEmailAsync(Guid user, CancellationToken ct) => Task.FromResult<string?>(null);
        public async Task<SecurityEmail?> FindSecurityMailAsync(Guid user, Guid ticket, CancellationToken ct)
        {
            await using var scope = source.CreateAsyncScope();
            var mail = await scope.ServiceProvider.GetRequiredService<IAccountProvisioning>().MailAsync(user, ticket, ct);
            return mail is null ? null : new(mail.UserId, mail.Address, mail.Subject, mail.Body);
        }
    }
    private const string Password = "Correct!LongPassword-123";
    private static ServiceProvider Services(TestDatabase catalog)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["ConnectionStrings:Database"] = catalog.ConnectionString, ["Identity:PublicWebAddress"] = "https://web.tests.invalid" }).Build();
        return new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(config).AddInfrastructure(config)
            .AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()).BuildServiceProvider();
    }
    [SqlServerFact]
    public async Task Concurrent_bootstrap_provisions_exactly_one_Admin_and_records_operator_service_audit()
    {
        await using var catalog = await TestDatabase.CreateAsync("Identity"); await using var services = Services(catalog);
        await using (var scope = services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        async Task<bool> Provision(int number)
        {
            await using var scope = services.CreateAsyncScope();
            try { await scope.ServiceProvider.GetRequiredService<IAdminProvisioning>().ProvisionAsync("admin" + number, $"admin{number}@tests.invalid", Password, default); return true; }
            catch (InvalidOperationException) { return false; }
        }
        Assert.Equal(1, (await Task.WhenAll(Provision(1), Provision(2))).Count(x => x));
        await using var check = services.CreateAsyncScope(); var db = check.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Single(await db.Users.ToArrayAsync()); Assert.Single(await db.UserRoles.ToArrayAsync());
        var audit = await db.AuditEntries.SingleAsync(x => x.Action == "AdminProvisioned"); Assert.Null(audit.ActorUserId); Assert.Equal("Identity", audit.ActorService);
        Assert.False((await db.Users.SingleAsync()).TwoFactorEnabled);
        Assert.Null(await check.ServiceProvider.GetRequiredService<IIdentitySessions>().LoginAsync("admin1", Password, default));
    }
    [SqlServerFact]
    public async Task Disabled_pending_invitation_cannot_activate_or_resend_until_admin_resumes_and_issues_fresh_link()
    {
        await using var catalog = await TestDatabase.CreateAsync("Identity");
        await using var services = Services(catalog); await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); await db.Database.MigrateAsync();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = ApplicationUser.CreateAdmin("admin", "admin@tests.invalid");
        Assert.True((await manager.CreateAsync(admin, Password)).Succeeded); Assert.True((await manager.AddToRoleAsync(admin, "Admin")).Succeeded);
        var mfa = scope.ServiceProvider.GetRequiredService<MfaSecurity>();
        var setup = (await mfa.SetupAsync("admin", Password, "Totp", null, null, default))!;
        var recovery = (await mfa.ConfirmAsync(admin.Id, setup.SetupToken, MfaSecurity.Totp(setup.Secret!, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30), default))!;
        var sessions = scope.ServiceProvider.GetRequiredService<IIdentitySessions>();
        var actor = (await sessions.LoginAsync("admin", Password, default, recoveryCode: recovery[0]))!.User;
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountProvisioning>();
        var invited = await accounts.InviteAsync(new("pending@tests.invalid", "Pending", "Technician", null, null), actor.Id, default);
        Assert.Equal("Created", invited.Status); var id = invited.UserId!.Value;
        var tickets = scope.ServiceProvider.GetRequiredService<SecurityTickets>();
        async Task<(Guid Id, string Raw)> CurrentLink()
        {
            var message = await db.OutboxMessages.Where(x => x.EventType == IdentityMailEvent.Requested).OrderByDescending(x => x.OccurredAtUtc).FirstAsync();
            var payload = JsonSerializer.Deserialize<IdentityMailEvent>(message.PayloadJson)!;
            return (payload.TicketId, tickets.Unprotect((await tickets.ReadAsync(id, payload.TicketId, default))!.ProtectedSecret));
        }
        async Task<ApplicationUser> Current()
        { db.ChangeTracker.Clear(); return await db.Users.AsNoTracking().SingleAsync(x => x.Id == id); }
        var oldLink = await CurrentLink(); var commands = scope.ServiceProvider.GetRequiredService<EmployeeCommands>();
        Assert.Equal(EmployeeChangeStatus.Saved, await commands.AccessAsync(id, new(false, (await Current()).RowVersion), actor, "suspend-test", default));
        Assert.True((await Current()).IsInvitationSuspended);
        Assert.False(await accounts.ActivateAsync(id, oldLink.Id, oldLink.Raw, Password, default));
        Assert.Equal("Conflict", (await accounts.ResendAsync(id, actor.Id, (await Current()).RowVersion, default)).Status);
        var priorMigration = db.Database.GetMigrations().Reverse().Skip(1).First();
        Assert.Equal(51075, (await Assert.ThrowsAsync<SqlException>(() => db.GetService<IMigrator>().MigrateAsync(priorMigration))).Number);
        Assert.True((await Current()).IsInvitationSuspended);
        Assert.Equal(EmployeeChangeStatus.Saved, await commands.AccessAsync(id, new(true, (await Current()).RowVersion), actor, "resume-test", default));
        var resumed = await Current(); Assert.False(resumed.IsActive); Assert.False(resumed.IsInvitationSuspended);
        Assert.Null(await sessions.LoginAsync("pending@tests.invalid", Password, default));
        Assert.False(await accounts.ActivateAsync(id, oldLink.Id, oldLink.Raw, Password, default));
        Assert.Equal("Created", (await accounts.ResendAsync(id, actor.Id, resumed.RowVersion, default)).Status);
        Assert.Equal("Conflict", (await accounts.ResendAsync(id, actor.Id, resumed.RowVersion, default)).Status);
        var freshLink = await CurrentLink(); Assert.NotEqual(oldLink.Id, freshLink.Id);
        Assert.True(await accounts.ActivateAsync(id, freshLink.Id, freshLink.Raw, Password, default));
        Assert.NotNull(await sessions.LoginAsync("pending@tests.invalid", Password, default));
    }
    [RedisSqlFact]
    public async Task Activation_outbox_real_Redis_inbox_and_SMTP_deliver_without_storing_plaintext_secret()
    {
        await using var redis = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("REDIS_TEST_CONNECTION")!);
        RedisKey[] keys = [IdentityMailEvent.Stream, IdentityMailEvent.Stream + ":dead-letter"];
        foreach (var key in keys) Assert.False(await redis.GetDatabase().KeyExistsAsync(key), "Use dedicated empty test streams.");
        try
        {
            await using var sourceCatalog = await TestDatabase.CreateAsync("Identity");
            await using var targetCatalog = await TestDatabase.CreateAsync("Notification");
            await using var sourceServices = Services(sourceCatalog);
            await using var sourceScope = sourceServices.CreateAsyncScope();
            var source = sourceScope.ServiceProvider.GetRequiredService<IdentityDbContext>(); await source.Database.MigrateAsync();
            var accounts = sourceScope.ServiceProvider.GetRequiredService<IAccountProvisioning>();
            var invited = await accounts.InviteAsync(new("invited@tests.invalid", "Invited", "Technician", null, null), Guid.NewGuid(), default);
            Assert.Equal("Created", invited.Status);
            var outbox = await source.OutboxMessages.AsNoTracking().SingleAsync();
            var payload = JsonSerializer.Deserialize<IdentityMailEvent>(outbox.PayloadJson)!;
            var ticketStore = sourceScope.ServiceProvider.GetRequiredService<SecurityTickets>();
            var raw = ticketStore.Unprotect((await ticketStore.ReadAsync(payload.UserId, payload.TicketId, default))!.ProtectedSecret);
            Assert.DoesNotContain(raw, outbox.PayloadJson);
            var directory = new SecurityDirectory(sourceServices);
            await using var targetServices = new ServiceCollection().AddSingleton<IConnectionMultiplexer>(redis)
                .AddSingleton<IRecipientEmailDirectory>(directory).AddSingleton(TimeProvider.System)
                .AddDbContext<NotificationDbContext>(options => options.UseSqlServer(targetCatalog.ConnectionString)).BuildServiceProvider();
            await using (var scope = targetServices.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync();
            var publisher = new MedEquipSLA.Identity.Infrastructure.Messaging.RedisEventPublisher(redis);
            await MedEquipSLA.Identity.Infrastructure.Workers.IdentityOutboxDispatcher.PublishAsync(source, publisher, default);
            using var consumer = new NotificationConsumer(new ConfigurationBuilder().Build(), targetServices.GetRequiredService<IServiceScopeFactory>(), NullLogger<NotificationConsumer>.Instance);
            await consumer.PollAsync(IdentityMailEvent.Stream, "first", "0-0", default);
            await publisher.PublishAsync(IdentityMailEvent.Stream, new(outbox.EventId, outbox.EventType, 1, "Identity", outbox.AggregateId,
                outbox.AggregateVersion, outbox.OccurredAtUtc, outbox.CorrelationId, outbox.PayloadJson), default);
            await consumer.PollAsync(IdentityMailEvent.Stream, "restarted", "0-0", default);
            await using var target = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>().UseSqlServer(targetCatalog.ConnectionString).Options);
            Assert.Single(await target.InboxMessages.ToArrayAsync()); Assert.Single(await target.EmailDeliveries.ToArrayAsync());
            var notification = await target.Notifications.SingleAsync(); Assert.False(notification.IsInAppVisible);
            Assert.DoesNotContain(raw, notification.Body); Assert.DoesNotContain(raw, (await target.EmailDeliveries.SingleAsync()).Body);
            await using var smtp = new EmailDeliveryTests.SmtpCapture();
            var sender = new SmtpEmailSender(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["Email:Host"] = "127.0.0.1", ["Email:Port"] = smtp.Port.ToString(), ["Email:From"] = "sender@tests.invalid", ["Email:EnableSsl"] = "false" }).Build());
            var queue = new EmailDeliveryQueue(target, directory, sender, TimeProvider.System, new());
            Assert.Equal(1, await queue.SendBatchAsync(default)); Assert.Single(smtp.Messages);
            var wire = smtp.Messages.Single();
            Assert.True(wire.Contains("Content-Transfer-Encoding: base64", StringComparison.OrdinalIgnoreCase));
            var deliveredBody = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(wire[(wire.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..]));
            Assert.True(deliveredBody.Contains("/activate?", StringComparison.Ordinal) && deliveredBody.Contains(raw, StringComparison.Ordinal),
                "SMTP accepted the rendered activation message.");
            Assert.Equal("Sent", (await target.EmailDeliveries.AsNoTracking().SingleAsync()).Status);
            Assert.Equal(0, (await redis.GetDatabase().StreamPendingAsync(IdentityMailEvent.Stream, NotificationConsumer.IdentityMailGroup)).PendingMessageCount);
        }
        finally { await redis.GetDatabase().KeyDeleteAsync(keys); }
    }
    [Fact]
    public void Totp_matches_RFC6238_SHA1_vectors_and_rejects_reused_step()
    {
        const string secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
        Assert.Equal("287082", MfaSecurity.Totp(secret, 59 / 30));
        Assert.Equal("081804", MfaSecurity.Totp(secret, 1111111109 / 30));
        Assert.Equal("279037", MfaSecurity.Totp(secret, 2000000000L / 30));
        Assert.Null(MfaSecurity.MatchStep(secret, "287082", DateTimeOffset.FromUnixTimeSeconds(59), 1));
    }
    [SqlServerFact]
    public async Task Admin_requires_TOTP_enrollment_and_recovery_codes_are_single_use_and_encrypted()
    {
        await using var catalog = await TestDatabase.CreateAsync("Identity");
        await using var services = Services(catalog); await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); await db.Database.MigrateAsync();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = ApplicationUser.CreateAdmin("admin", "admin@tests.invalid");
        Assert.True((await manager.CreateAsync(admin, Password)).Succeeded); Assert.True((await manager.AddToRoleAsync(admin, "Admin")).Succeeded);
        var sessions = scope.ServiceProvider.GetRequiredService<IIdentitySessions>();
        Assert.Null(await sessions.LoginAsync("admin", Password, default));
        Assert.Empty(await db.RefreshTokens.ToArrayAsync());
        var mfa = scope.ServiceProvider.GetRequiredService<MfaSecurity>();
        Assert.Null(await mfa.SetupAsync("admin", Password, "Email", null, null, default));
        var setup = await mfa.SetupAsync("admin", Password, "Totp", null, null, default); Assert.NotNull(setup);
        var code = MfaSecurity.Totp(setup.Secret!, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        var recovery = await mfa.ConfirmAsync(setup.UserId, setup.SetupToken, code, default); Assert.NotNull(recovery); Assert.Equal(10, recovery.Length);
        Assert.Null(await mfa.ConfirmAsync(setup.UserId, setup.SetupToken, code, default));
        Assert.Null(await sessions.LoginAsync("admin", Password, default, code));
        var grant = await sessions.LoginAsync("admin", Password, default, recoveryCode: recovery[0]); Assert.NotNull(grant);
        Assert.True(await sessions.IsCurrentAsync(grant.User, default));
        Assert.Null(await sessions.LoginAsync("admin", Password, default, recoveryCode: recovery[0]));
        Assert.False(await mfa.DisableAsync("admin", Password, null, recovery[1], default));
        foreach (var value in await db.UserTokens.Select(x => x.Value!).ToArrayAsync())
        { Assert.DoesNotContain(setup.Secret!, value); Assert.DoesNotContain(recovery[0], value); }
    }
    [SqlServerFact]
    public async Task Invitation_resend_activation_and_password_reset_are_single_use_and_revoke_sessions()
    {
        await using var catalog = await TestDatabase.CreateAsync("Identity");
        await using var services = Services(catalog); await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); await db.Database.MigrateAsync();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountProvisioning>();
        var actor = Guid.NewGuid(); var invitation = new EmployeeInvitation("tech@tests.invalid", "Technician", "Technician", null, null);
        Assert.Equal("Invalid", (await accounts.InviteAsync(invitation with { Role = "Admin" }, actor, default)).Status);
        var result = await accounts.InviteAsync(invitation, actor, default); Assert.Equal("Created", result.Status);
        Assert.Equal("Duplicate", (await accounts.InviteAsync(invitation with { Email = "TECH@tests.invalid" }, actor, default)).Status);
        var sessions = scope.ServiceProvider.GetRequiredService<IIdentitySessions>();
        Assert.Null(await sessions.LoginAsync(invitation.Email, Password, default));
        var original = JsonSerializer.Deserialize<IdentityMailEvent>((await db.OutboxMessages.SingleAsync()).PayloadJson)!;
        var row = await db.Users.AsNoTracking().SingleAsync();
        Assert.Equal("Created", (await accounts.ResendAsync(row.Id, actor, row.RowVersion, default)).Status);
        Assert.Null(await accounts.MailAsync(row.Id, original.TicketId, default));
        var envelope = await db.OutboxMessages.OrderByDescending(x => x.OccurredAtUtc).FirstAsync();
        var payload = JsonSerializer.Deserialize<IdentityMailEvent>(envelope.PayloadJson)!;
        var tickets = scope.ServiceProvider.GetRequiredService<SecurityTickets>();
        var ticket = await tickets.ReadAsync(row.Id, payload.TicketId, default); Assert.NotNull(ticket);
        var raw = tickets.Unprotect(ticket.ProtectedSecret);
        Assert.DoesNotContain(raw, envelope.PayloadJson); Assert.NotNull(await accounts.MailAsync(row.Id, payload.TicketId, default));
        Assert.False(await accounts.ActivateAsync(row.Id, payload.TicketId, raw, "short", default));
        Assert.True(await accounts.ActivateAsync(row.Id, payload.TicketId, raw, Password, default));
        Assert.False(await accounts.ActivateAsync(row.Id, payload.TicketId, raw, Password, default));
        var grant = await sessions.LoginAsync(invitation.Email, Password, default); Assert.NotNull(grant);
        await accounts.RequestResetAsync(invitation.Email, default);
        var reset = JsonSerializer.Deserialize<IdentityMailEvent>((await db.OutboxMessages.OrderByDescending(x => x.OccurredAtUtc).FirstAsync()).PayloadJson)!;
        var resetTicket = (await tickets.ReadAsync(row.Id, reset.TicketId, default))!;
        Assert.True(await accounts.ResetAsync(row.Id, reset.TicketId, tickets.Unprotect(resetTicket.ProtectedSecret), Password + "New", default));
        Assert.False(await sessions.IsCurrentAsync(grant.User, default));
        Assert.Null(await sessions.RefreshAsync(grant.RefreshToken, default));
        Assert.NotNull(await sessions.LoginAsync(invitation.Email, Password + "New", default));
    }
    [SqlServerFact]
    public async Task Email_MFA_setup_login_OTP_replay_and_staff_disable_require_proof()
    {
        await using var catalog = await TestDatabase.CreateAsync("Identity");
        await using var services = Services(catalog); await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); await db.Database.MigrateAsync();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = ApplicationUser.CreateAdmin("tech", "tech@tests.invalid");
        Assert.True((await manager.CreateAsync(user, Password)).Succeeded); Assert.True((await manager.AddToRoleAsync(user, "Technician")).Succeeded);
        var mfa = scope.ServiceProvider.GetRequiredService<MfaSecurity>(); var tickets = scope.ServiceProvider.GetRequiredService<SecurityTickets>();
        var setup = await mfa.SetupAsync("tech", Password, "Email", null, null, default); Assert.NotNull(setup); Assert.Null(setup.Secret);
        async Task<string> LatestCode()
        {
            var payload = JsonSerializer.Deserialize<IdentityMailEvent>((await db.OutboxMessages.OrderByDescending(x => x.OccurredAtUtc).FirstAsync()).PayloadJson)!;
            return tickets.Unprotect((await tickets.ReadAsync(user.Id, payload.TicketId, default))!.ProtectedSecret);
        }
        var recovery = await mfa.ConfirmAsync(user.Id, setup.SetupToken, await LatestCode(), default); Assert.NotNull(recovery);
        Assert.True(await mfa.EmailCodeAsync("tech", Password, default)); var code = await LatestCode();
        var sessions = scope.ServiceProvider.GetRequiredService<IIdentitySessions>();
        var grant = await sessions.LoginAsync("tech", Password, default, code); Assert.NotNull(grant);
        Assert.Null(await sessions.LoginAsync("tech", Password, default, code));
        Assert.False(await mfa.DisableAsync("tech", Password, "000000", null, default));
        Assert.True(await mfa.DisableAsync("tech", Password, null, recovery[0], default));
        Assert.False(await sessions.IsCurrentAsync(grant.User, default));
        Assert.NotNull(await sessions.LoginAsync("tech", Password, default));
    }
}
