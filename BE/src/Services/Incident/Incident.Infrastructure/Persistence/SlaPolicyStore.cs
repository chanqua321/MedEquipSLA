using MedEquipSLA.Incident.Application.Features.Sla;
using MedEquipSLA.Incident.Domain;
using MedEquipSLA.Incident.Infrastructure.Persistence.Entities;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace MedEquipSLA.Incident.Infrastructure.Persistence;

public sealed class SlaPolicyStore(IncidentDbContext db) : ISlaPolicyStore
{
    public async Task<IReadOnlyList<SlaPolicyView>> ListAsync(CancellationToken ct) =>
        await db.SlaPolicies.AsNoTracking().OrderBy(x => x.Priority).Select(x => new SlaPolicyView(x.Priority, x.IsEnabled, x.RowVersion,
            db.SlaPolicyVersions.Where(v => v.SlaPolicyId == x.Id).OrderByDescending(v => v.Version)
                .Select(v => new SlaVersionView(v.Version, v.ResponseDurationMinutes, v.ResolutionDurationMinutes)).FirstOrDefault())).ToArrayAsync(ct);
    public async Task<SlaPolicyResult> ConfigureAsync(string priority, SlaPolicyInput input, Guid actor, string correlation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var policy = await db.SlaPolicies.FromSqlInterpolated(
            $"SELECT * FROM dbo.SlaPolicies WITH (UPDLOCK, HOLDLOCK) WHERE Priority = {priority}")
            .SingleOrDefaultAsync(ct);
        if (policy is not null && (input.RowVersion is null || !policy.RowVersion.SequenceEqual(input.RowVersion)))
            return SlaPolicyResult.Conflict;
        if (policy is null && (!input.Enabled || input.RowVersion is not null))
            return SlaPolicyResult.Conflict;
        var now = DateTimeOffset.UtcNow;
        if (policy is null)
        {
            policy = SlaPolicy.Create(priority, actor, now);
            db.SlaPolicies.Add(policy);
        }
        var latest = await db.SlaPolicyVersions.Where(x => x.SlaPolicyId == policy.Id)
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (latest is { EffectiveToUtc: null })
        {
            if (latest.EffectiveFromUtc >= now) return SlaPolicyResult.Conflict;
            latest.End(now);
            // Close the old filtered-unique row before inserting its replacement.
            await db.SaveChangesAsync(ct);
        }
        policy.Configure(input.Enabled, actor, now);
        if (input.Enabled)
            db.SlaPolicyVersions.Add(SlaPolicyVersion.Start(policy.Id, (latest?.Version ?? 0) + 1,
                input.ResponseDurationMinutes, input.ResolutionDurationMinutes, actor, now));
        db.Add(AuditEntry.ForIncident(actor, policy.Id, "SlaPolicyConfigured", now, correlation,
            JsonSerializer.Serialize(new { priority, input.Enabled, input.ResponseDurationMinutes, input.ResolutionDurationMinutes,
                previousVersion = latest?.Version, nextVersion = input.Enabled ? (latest?.Version ?? 0) + 1 : latest?.Version })));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return SlaPolicyResult.Saved;
    }
}
