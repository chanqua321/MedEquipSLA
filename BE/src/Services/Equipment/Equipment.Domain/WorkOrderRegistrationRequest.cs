using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MedEquipSLA.Equipment.Domain;

/// <summary>Versioned canonical business request; no transport credentials, leases or fences.</summary>
public sealed record WorkOrderRegistrationRequest(Guid EquipmentId, Guid ScheduleId, Guid OccurrenceId,
    Guid WorkOrderId, string WorkType, DateTimeOffset ScheduledForUtc, DateTimeOffset DueAtUtc)
{
    public string CanonicalJson()
    {
        if (EquipmentId == Guid.Empty || ScheduleId == Guid.Empty || OccurrenceId == Guid.Empty || WorkOrderId == Guid.Empty ||
            WorkType is not ("Maintenance" or "Calibration") || DueAtUtc < ScheduledForUtc)
            throw new InvalidOperationException("Invalid work order registration request.");
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteString("equipmentId", EquipmentId.ToString("D"));
            writer.WriteString("scheduleId", ScheduleId.ToString("D"));
            writer.WriteString("occurrenceId", OccurrenceId.ToString("D"));
            writer.WriteString("workOrderId", WorkOrderId.ToString("D"));
            writer.WriteString("workType", WorkType);
            writer.WriteString("scheduledForUtc", ScheduledForUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("dueAtUtc", DueAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    public byte[] Hash() => SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalJson()));
}
