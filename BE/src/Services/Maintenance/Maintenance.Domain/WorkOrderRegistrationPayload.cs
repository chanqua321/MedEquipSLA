using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MedEquipSLA.Maintenance.Domain;

public sealed record WorkOrderRegistrationPayload(Guid EquipmentId, Guid ScheduleId, Guid OccurrenceId,
    Guid WorkOrderId, string WorkType, DateTimeOffset ScheduledForUtc, DateTimeOffset DueAtUtc)
{
    // Wire contract canonical v1. Equipment independently validates the same bytes.
    public string CanonicalJson()
    {
        if (EquipmentId == Guid.Empty || ScheduleId == Guid.Empty || OccurrenceId == Guid.Empty || WorkOrderId == Guid.Empty ||
            WorkType is not ("Maintenance" or "Calibration") || DueAtUtc < ScheduledForUtc)
            throw new InvalidOperationException("Invalid work order registration payload.");
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
    public static WorkOrderRegistrationPayload Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != 1) throw new InvalidOperationException("Unknown payload version.");
        var payload = new WorkOrderRegistrationPayload(root.GetProperty("equipmentId").GetGuid(), root.GetProperty("scheduleId").GetGuid(),
            root.GetProperty("occurrenceId").GetGuid(), root.GetProperty("workOrderId").GetGuid(), root.GetProperty("workType").GetString()!,
            root.GetProperty("scheduledForUtc").GetDateTimeOffset(), root.GetProperty("dueAtUtc").GetDateTimeOffset());
        if (payload.CanonicalJson() != json) throw new InvalidOperationException("Payload is not canonical.");
        return payload;
    }
}
