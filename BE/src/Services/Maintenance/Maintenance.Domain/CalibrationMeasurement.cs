namespace MedEquipSLA.Maintenance.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class CalibrationMeasurement
{
    public static CalibrationMeasurement Record(Guid attempt, string code, string name, string unit, decimal value,
        decimal? minimum, decimal? maximum)
    {
        if (code.Trim().Length is < 1 or > 50 || name.Trim().Length is < 1 or > 200 || unit.Trim().Length is < 1 or > 30 ||
            minimum is null && maximum is null || minimum > maximum ||
            new[] { (decimal?)value, minimum, maximum }.Any(x => x is { } v &&
                (v <= -1_000_000_000_000m || v >= 1_000_000_000_000m || decimal.Round(v, 6) != v)))
            throw new InvalidOperationException("Valid calibration parameter, limits and precision are required.");
        return new() { WorkOrderAttemptId = attempt, ParameterCode = code.Trim().ToUpperInvariant(), ParameterName = name.Trim(),
            Unit = unit.Trim(), MeasuredValue = value, MinimumAllowed = minimum, MaximumAllowed = maximum,
            Passed = (minimum is null || value >= minimum) && (maximum is null || value <= maximum) };
    }
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
