using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MedEquipSLA.Contracts.Events;
using MedEquipSLA.Identity.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Identity.Infrastructure.Persistence;

// ASP.NET Identity token storage owns single-use authentication artifacts. Values are encrypted at rest.
public sealed class SecurityTickets(IdentityDbContext db, IDataProtectionProvider protection)
{
    public const string Provider = "MedEquipSLA.Security.v1";
    public sealed record Ticket(Guid Id, string Kind, string TokenHash, string ProtectedSecret, DateTimeOffset ExpiresAtUtc,
        long AuthorizationVersion, int Attempts = 0, bool Used = false);
    private readonly IDataProtector protector = protection.CreateProtector("MedEquipSLA.Identity.Security.v1");
    public string Protect(string value) => protector.Protect(value);
    public string Unprotect(string value) => protector.Unprotect(value);
    public async Task<ApplicationUser?> LockAsync(Guid user, CancellationToken ct)
    {
        // A request may follow an ExecuteUpdate or rolled-back authentication attempt in the same scope.
        // Reload all security state only after obtaining the serializing user lock.
        db.ChangeTracker.Clear();
        return await db.Users.FromSqlInterpolated(
            $"SELECT * FROM dbo.AspNetUsers WITH (UPDLOCK,HOLDLOCK) WHERE Id={user}").SingleOrDefaultAsync(ct);
    }
    public async Task<Ticket?> ReadAsync(Guid user, Guid ticket, CancellationToken ct)
    {
        var row = await db.UserTokens.SingleOrDefaultAsync(x => x.UserId == user && x.LoginProvider == Provider && x.Name == ticket.ToString(), ct);
        return row?.Value is { } value ? JsonSerializer.Deserialize<Ticket>(Unprotect(value)) : null;
    }
    public async Task SaveAsync(Guid user, Ticket ticket, CancellationToken ct)
    {
        var row = await db.UserTokens.SingleOrDefaultAsync(x => x.UserId == user && x.LoginProvider == Provider && x.Name == ticket.Id.ToString(), ct);
        if (row is null) { row = new() { UserId = user, LoginProvider = Provider, Name = ticket.Id.ToString() }; db.Add(row); }
        row.Value = Protect(JsonSerializer.Serialize(ticket));
    }
    public async Task<Ticket> CreateAsync(ApplicationUser user, string kind, TimeSpan lifetime, CancellationToken ct, string? secret = null)
    {
        // Resend invalidates every previous ticket of this kind, including queued email rendering.
        var rows = await db.UserTokens.Where(x => x.UserId == user.Id && x.LoginProvider == Provider).ToArrayAsync(ct);
        foreach (var row in rows)
        {
            var old = JsonSerializer.Deserialize<Ticket>(Unprotect(row.Value!))!;
            if (old.Kind == kind && !old.Used) row.Value = Protect(JsonSerializer.Serialize(old with { Used = true }));
        }
        var raw = secret ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var ticket = new Ticket(Guid.NewGuid(), kind, Hash(raw), Protect(raw), DateTimeOffset.UtcNow.Add(lifetime), user.AuthorizationVersion);
        await SaveAsync(user.Id, ticket, ct);
        var payload = new IdentityMailEvent(user.Id, ticket.Id);
        db.Add(new OutboxMessage(new(Guid.NewGuid(), IdentityMailEvent.Requested, 1, "Identity", ticket.Id, 1,
            DateTimeOffset.UtcNow, ticket.Id.ToString(), JsonSerializer.Serialize(payload))));
        return ticket;
    }
    public static bool Valid(Ticket? ticket, ApplicationUser user, string kind, string token) => ticket is { Used: false, Attempts: < 5 } &&
        ticket.Kind == kind && ticket.ExpiresAtUtc > DateTimeOffset.UtcNow && ticket.AuthorizationVersion == user.AuthorizationVersion &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(ticket.TokenHash), Encoding.ASCII.GetBytes(Hash(token)));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
