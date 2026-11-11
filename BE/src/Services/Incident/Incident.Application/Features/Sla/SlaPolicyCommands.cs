namespace MedEquipSLA.Incident.Application.Features.Sla;

public sealed record SlaPolicyInput(bool Enabled, int ResponseDurationMinutes, int ResolutionDurationMinutes, byte[]? RowVersion);
public sealed record SlaVersionView(int Version, int ResponseDurationMinutes, int ResolutionDurationMinutes);
public sealed record SlaPolicyView(string Priority, bool IsEnabled, byte[] RowVersion, SlaVersionView? Version);
public enum SlaPolicyResult { Saved, Invalid, Conflict }
public interface ISlaPolicyStore
{
    Task<IReadOnlyList<SlaPolicyView>> ListAsync(CancellationToken ct);
    Task<SlaPolicyResult> ConfigureAsync(string priority, SlaPolicyInput input, Guid actor, string correlation, CancellationToken ct);
}
public sealed class SlaPolicyQueries(ISlaPolicyStore store)
{
    public Task<IReadOnlyList<SlaPolicyView>> HandleAsync(CancellationToken ct) => store.ListAsync(ct);
}
public sealed class SlaPolicyCommands(ISlaPolicyStore store)
{
    public Task<SlaPolicyResult> HandleAsync(string priority, SlaPolicyInput input, Guid actor, string correlation, CancellationToken ct) =>
        actor == Guid.Empty || priority is not ("Critical" or "High" or "Medium" or "Low") || input.RowVersion is { Length: not 8 } ||
        input.Enabled && (input.ResponseDurationMinutes <= 0 || input.ResolutionDurationMinutes < input.ResponseDurationMinutes)
        ? Task.FromResult(SlaPolicyResult.Invalid) : store.ConfigureAsync(priority, input, actor, correlation, ct);
}
