namespace MedEquipSLA.Maintenance.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class CalibrationMeasurement
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkOrderAttemptId { get; private set; }
    public string ParameterCode { get; private set; } = string.Empty;
    public string ParameterName { get; private set; } = string.Empty;
    public string Unit { get; private set; } = string.Empty;
    public decimal MeasuredValue { get; private set; }
    public decimal? MinimumAllowed { get; private set; }
    public decimal? MaximumAllowed { get; private set; }
    public bool Passed { get; private set; }
}
