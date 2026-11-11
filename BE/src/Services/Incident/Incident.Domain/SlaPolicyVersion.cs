namespace MedEquipSLA.Incident.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class SlaPolicyVersion
{
    public static SlaPolicyVersion Start(Guid policy, int version, int response, int resolution,
        Guid actor, DateTimeOffset now) => new()
    {
        SlaPolicyId = policy, Version = version, ResponseDurationMinutes = response,
        ResolutionDurationMinutes = resolution, CreatedByUserId = actor, CreatedAtUtc = now, EffectiveFromUtc = now
    };

    public void End(DateTimeOffset now) => EffectiveToUtc = now;

    public Guid SlaPolicyId { get; private set; }
    public int Version { get; private set; }
    public int ResponseDurationMinutes { get; private set; }
    public int ResolutionDurationMinutes { get; private set; }
    public DateTimeOffset EffectiveFromUtc { get; private set; }
    public DateTimeOffset? EffectiveToUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
