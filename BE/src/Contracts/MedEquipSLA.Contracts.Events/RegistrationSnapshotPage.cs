using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MedEquipSLA.Contracts.Events;

// Positive source evidence only. Absence from an inventory never releases a registration.
public sealed record RegistrationSnapshotItem(string Kind, Guid ResourceId, Guid OperationId,
    Guid? EquipmentId, Guid? RegistrationId, string State, long Version, string Status,
    long FencingToken = 0, string? RegistrationPayloadJson = null, bool ScheduleReleaseConfirmed = false);

public sealed record RegistrationSnapshotPage(Guid SnapshotId, DateTimeOffset CapturedAtUtc, int Index,
    int TotalItems, string[] PageHashes, RegistrationSnapshotItem[] Items)
{
    public const string EventType = "RegistrationSnapshotPage";
    public const string Stream = "medequip:registration:snapshots:v1";
    public string Watermark => $"{CapturedAtUtc.UtcTicks}:{TotalItems}:{PageHashes.Length}:{Hash(PageHashes)}";
    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public static RegistrationSnapshotPage[] Capture(IEnumerable<RegistrationSnapshotItem> items, DateTimeOffset now, int size = 100)
    {
        if (size is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(size));
        var all = items.OrderBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.ResourceId.ToString(), StringComparer.Ordinal)
            .ThenBy(x => x.OperationId.ToString(), StringComparer.Ordinal).ToArray();
        var chunks = all.Length == 0 ? [Array.Empty<RegistrationSnapshotItem>()] : all.Chunk(size).ToArray();
        var hashes = chunks.Select(Hash).ToArray(); var id = Guid.NewGuid();
        return chunks.Select((chunk, index) => new RegistrationSnapshotPage(id, now.ToUniversalTime(), index, all.Length, hashes, chunk)).ToArray();
    }
    public IntegrationEvent Envelope(string source)
    {
        var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{SnapshotId:N}:{Index}")).AsSpan(0,16));
        return new(id, EventType, 1, source, SnapshotId, Index+1, CapturedAtUtc,
            SnapshotId.ToString(), JsonSerializer.Serialize(this));
    }
    public void Validate()
    {
        if (SnapshotId == Guid.Empty || CapturedAtUtc.Offset != TimeSpan.Zero || TotalItems < 0 ||
            PageHashes is not { Length: > 0 } || Index < 0 || Index >= PageHashes.Length || Items is null || Items.Length > 100 ||
            PageHashes.Any(x => x is not { Length: 64 } || !x.All(Uri.IsHexDigit)) || Hash(Items) != PageHashes[Index] ||
            Items.Any(x => x.ResourceId == Guid.Empty || x.State is not ("Pending" or "Committed" or "Aborted" or "Unknown")))
            throw new JsonException("Invalid registration snapshot page.");
    }
}
