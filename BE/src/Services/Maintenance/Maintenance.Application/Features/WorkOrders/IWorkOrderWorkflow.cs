namespace MedEquipSLA.Maintenance.Application.Features.WorkOrders;

public sealed record CalibrationInput(string Code, string Name, string Unit, decimal Value, decimal? Minimum, decimal? Maximum);
public sealed record WorkOrderReport(string Result, string Notes, decimal? Cost, string? Currency, byte[] Photo,
    string ContentType, IReadOnlyList<CalibrationInput> Measurements);
public enum WorkflowStatus { Saved, Invalid, Forbidden, NotFound, Conflict, Unavailable }
public interface IWorkOrderWorkflow
{
    Task<WorkflowStatus> ProgressAsync(Guid id, Guid actor, byte[] version, string notes, CancellationToken ct);
    Task<WorkflowStatus> StartAsync(Guid id, Guid actor, byte[] version, CancellationToken ct);
    Task<WorkflowStatus> SubmitAsync(Guid id, Guid actor, byte[] version, WorkOrderReport report, CancellationToken ct);
    Task<WorkflowStatus> ReviewAsync(Guid id, Guid actor, byte[] version, bool accept, string? reason, CancellationToken ct);
    Task<WorkflowStatus> CancelAsync(Guid id, Guid actor, byte[] version, string reason, CancellationToken ct);
}
