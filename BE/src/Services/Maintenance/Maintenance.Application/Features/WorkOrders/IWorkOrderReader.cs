namespace MedEquipSLA.Maintenance.Application.Features.WorkOrders;

public sealed record WorkOrderSearch(string? Search, string? Status, int Page);
public sealed record WorkOrderRow(Guid Id, string Code, string Equipment, string Department, string WorkType,
    string Status, DateTimeOffset ScheduledForUtc, DateTimeOffset DueAtUtc, bool IsBlocking,
    Guid? AssignedTechnicianId, byte[] RowVersion, Guid EquipmentId = default);
public sealed record WorkOrderPage(IReadOnlyList<WorkOrderRow> Items, bool HasNext);
public sealed record WorkOrderAttemptRow(int AttemptNumber, string ResultStatus, string Notes,
    DateTimeOffset CompletedAtUtc, string ReviewDecision, string? ReviewReason, Guid Id = default, bool HasPhoto = false);
public sealed record WorkOrderPhoto(byte[] Bytes, string ContentType);
public sealed record CalibrationMeasurementRow(string ParameterCode, string ParameterName, string Unit, decimal MeasuredValue,
    decimal? MinimumAllowed, decimal? MaximumAllowed, bool Passed);

public interface IWorkOrderReader
{
    Task<WorkOrderPage> ListAsync(WorkOrderSearch search, CancellationToken ct);
    Task<WorkOrderRow?> FindAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<WorkOrderAttemptRow>?> ReadAttemptsAsync(Guid id, CancellationToken ct);
    Task<WorkOrderPhoto?> PhotoAsync(Guid id, Guid attempt, Guid actor, bool admin, CancellationToken ct);
    Task<IReadOnlyList<CalibrationMeasurementRow>?> MeasurementsAsync(Guid id, Guid attempt, Guid actor, bool admin, CancellationToken ct);
}
