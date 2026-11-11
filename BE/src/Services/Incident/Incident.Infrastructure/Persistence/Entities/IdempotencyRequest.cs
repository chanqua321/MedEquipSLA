namespace MedEquipSLA.Incident.Infrastructure.Persistence.Entities;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class IdempotencyRequest
{
    public bool Claim(string owner, DateTimeOffset now)
    {
        if (State != "Pending" || InterventionRequired || NextRetryAtUtc > now || LeaseUntilUtc > now) return false;
        if (AttemptCount >= 8) { InterventionRequired = true; return false; }
        LeaseOwner = owner; LeaseUntilUtc = now.AddSeconds(30); FencingToken = checked(FencingToken + 1);
        AttemptCount++; TotalAttemptCount++; LastAttemptAtUtc = now; CurrentAttemptId = Guid.NewGuid(); UpdatedAtUtc = now;
        return true;
    }
    public bool Owns(string owner, long fence, DateTimeOffset now) =>
        State == "Pending" && LeaseOwner == owner && FencingToken == fence && LeaseUntilUtc > now;

    public void Defer(string error, DateTimeOffset now)
    {
        LastError = error.Length > 1000 ? error[..1000] : error;
        NextRetryAtUtc = now.AddSeconds(Math.Min(300, 5 * (1 << Math.Min(AttemptCount, 6))));
        InterventionRequired = AttemptCount >= 8;
        LeaseOwner = null; LeaseUntilUtc = null; UpdatedAtUtc = now;
    }
    public void RequestCancellation(Guid actor, string reason, DateTimeOffset now)
    {
        if (State != "Pending" || actor == Guid.Empty || reason.Trim().Length is < 1 or > 2000)
            throw new InvalidOperationException("Only unresolved operations may be cancelled.");
        if (CancelIntentId is not null) return;
        CancelIntentId = Guid.NewGuid(); CancelRequestedByUserId = actor; CancelReason = reason.Trim();
        FencingToken++; LeaseOwner = null; LeaseUntilUtc = null; NextRetryAtUtc = now; InterventionRequired = false;
        AttemptCount = 0; UpdatedAtUtc = now;
    }
    public void Abort(string proof, DateTimeOffset now)
    {
        if (State != "Pending" || CancelIntentId is null) throw new InvalidOperationException("Cancellation intent required.");
        State = "Aborted"; ResponseStatusCode = 200; ResponseJson = proof;
        LeaseOwner = null; LeaseUntilUtc = null; UpdatedAtUtc = now; LastError = null;
    }
    public void AuthorizeRetry(DateTimeOffset now)
    {
        if (State != "Pending") throw new InvalidOperationException("Only unresolved operations may retry.");
        FencingToken++; LeaseOwner = null; LeaseUntilUtc = null; AttemptCount = 0; NextRetryAtUtc = now;
        InterventionRequired = false; UpdatedAtUtc = now;
    }
    public int AttemptCount { get; private set; }
    public long TotalAttemptCount { get; private set; }
    public Guid? CurrentAttemptId { get; private set; }
    public DateTimeOffset? NextRetryAtUtc { get; private set; }
    public DateTimeOffset? LastAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseUntilUtc { get; private set; }
    public long FencingToken { get; private set; }
    public bool InterventionRequired { get; private set; }
    public Guid? CancelIntentId { get; private set; }
    public Guid? CancelRequestedByUserId { get; private set; }
    public string? CancelReason { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public static IdempotencyRequest Begin(string actor, string operation, string key, byte[] hash, DateTimeOffset now) => new()
    {
        ActorScope = actor,
        OperationName = operation,
        IdempotencyKey = key,
        RequestHash = hash,
        State = "Pending",
        CreatedAtUtc = now,
        ExpiresAtUtc = now.AddDays(7)
    };

    public void Commit(Guid resourceId)
    {
        if (State != "Pending" || CancelIntentId is not null) throw new InvalidOperationException("Only an uncancelled pending operation can commit.");
        State = "Committed";
        ResourceId = resourceId;
        ResponseStatusCode = 201;
    }

    public Guid? EquipmentId { get; private set; }
    public string? RequestPayloadJson { get; private set; }

    public void BindPendingRequest(Guid equipment, string payload, byte[] inputHash)
    {
        if (State != "Pending" || equipment == Guid.Empty ||
            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(RequestHash, inputHash) ||
            EquipmentId is { } bound && bound != equipment ||
            RequestPayloadJson is { } existing && existing != payload)
            throw new InvalidOperationException("Pending request binding mismatch.");
        EquipmentId = equipment;
        RequestPayloadJson = payload;
    }

    public Guid OperationId { get; private set; } = Guid.NewGuid();
    public string ActorScope { get; private set; } = string.Empty;
    public string OperationName { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public byte[] RequestHash { get; private set; } = [];
    public Guid? ResourceId { get; private set; }
    public string State { get; private set; } = string.Empty;
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseJson { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
