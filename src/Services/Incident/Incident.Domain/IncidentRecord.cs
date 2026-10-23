namespace MedEquipSLA.Incident.Domain;
public sealed class IncidentRecord
{
    public Guid Id { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    // Business fields and invariant methods are intentionally deferred until BR approval.
}
