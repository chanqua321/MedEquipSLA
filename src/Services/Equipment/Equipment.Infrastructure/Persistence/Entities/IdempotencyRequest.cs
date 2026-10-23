namespace MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class IdempotencyRequest
{
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
