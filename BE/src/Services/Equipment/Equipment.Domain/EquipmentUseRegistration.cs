namespace MedEquipSLA.Equipment.Domain;

// Persistence shape approved in ERD v0.1; use-case invariants belong in Domain/Application.
public sealed class EquipmentUseRegistration
{
    public static EquipmentUseRegistration ResourceAbortTombstone(EquipmentAsset asset, string departmentName,
        ResourceUseAbortRequest request, Guid cancel, long fence, DateTimeOffset now)
    {
        var row = Register(asset.Id, request.Source, request.ResourceType, request.OperationId, request.OperationId, now);
        row.AbortResource(asset, departmentName, request, cancel, fence, now);
        return row;
    }
    public bool AbortResource(EquipmentAsset asset, string departmentName, ResourceUseAbortRequest request, Guid cancel,
        long fence, DateTimeOffset now)
    {
        var json = request.CanonicalJson();
        if (asset.Id != EquipmentId || request.EquipmentId != EquipmentId || SourceService != request.Source ||
            ResourceType != request.ResourceType || ResourceId != request.OperationId || SourceOperationId != request.OperationId ||
            cancel == Guid.Empty || fence <= 0 || RequestHash is { } hash && !hash.SequenceEqual(request.RequestHash))
            throw new InvalidOperationException("Resource abort proof mismatch.");
        if (State == "Aborted")
        {
            if (CancelIntentId != cancel) throw new InvalidOperationException("Cancellation mismatch.");
            return false;
        }
        if (State != "Pending" || fence <= FencingToken) throw new InvalidOperationException("Only uncommitted registration can abort.");
        RequestHash = request.RequestHash; RequestPayloadJson = json; FencingToken = fence; CancelIntentId = cancel;
        EquipmentCodeSnapshot ??= asset.AssetCode; EquipmentNameSnapshot ??= asset.Name;
        DepartmentIdSnapshot ??= asset.DepartmentId; DepartmentNameSnapshot ??= departmentName;
        State = "Aborted"; LastVerifiedAtUtc = now.ToUniversalTime(); return true;
    }
    public bool ReleaseSchedule(Guid equipment, Guid schedule, Guid registration, long version, DateTimeOffset now)
    {
        if (EquipmentId != equipment || ResourceType != "MaintenanceSchedule" || SourceService != "Maintenance" ||
            ResourceId != schedule || Id != registration || version < 1 || State == "Aborted")
            throw new InvalidOperationException("Schedule release binding mismatch.");
        if (State == "Released") return false;
        if (version <= SourceVersion) throw new InvalidOperationException("Stale schedule terminal proof.");
        State = "Released"; SourceVersion = version; ReleasedAtUtc = now.ToUniversalTime(); LastVerifiedAtUtc = now.ToUniversalTime();
        return true;
    }
    public static EquipmentUseRegistration RegisterWorkOrder(EquipmentAsset asset, WorkOrderRegistrationRequest request,
        Guid operation, long fence, string departmentName, DateTimeOffset now) =>
        CreateWorkOrderRecord(asset, request, operation, fence, departmentName, now, null);

    public static EquipmentUseRegistration WorkOrderAbortTombstone(EquipmentAsset asset, WorkOrderRegistrationRequest request,
        Guid operation, long fence, string departmentName, DateTimeOffset now, Guid cancelIntent)
    {
        if (cancelIntent == Guid.Empty) throw new InvalidOperationException("A durable cancellation intent is required.");
        return CreateWorkOrderRecord(asset, request, operation, fence, departmentName, now, cancelIntent);
    }

    private static EquipmentUseRegistration CreateWorkOrderRecord(EquipmentAsset asset, WorkOrderRegistrationRequest request,
        Guid operation, long fence, string departmentName, DateTimeOffset now, Guid? cancelIntent)
    {
        if (asset.IsRetired && cancelIntent is null || asset.Id != request.EquipmentId || operation == Guid.Empty || fence <= 0 ||
            departmentName is not { Length: > 0 and <= 200 })
            throw new InvalidOperationException("Invalid equipment or registration identity.");
        var json = request.CanonicalJson();
        var result = Register(asset.Id, "Maintenance", "WorkOrder", request.OccurrenceId, operation, now.ToUniversalTime());
        result.RequestPayloadJson = json;
        result.RequestHash = request.Hash();
        result.FencingToken = fence;
        result.EquipmentCodeSnapshot = asset.AssetCode;
        result.EquipmentNameSnapshot = asset.Name;
        result.DepartmentIdSnapshot = asset.DepartmentId;
        result.DepartmentNameSnapshot = departmentName;
        if (cancelIntent is not null)
        {
            // Negative reservation, not a new right to use equipment. Late Register cannot revive it.
            result.State = "Aborted";
            result.CancelIntentId = cancelIntent;
            result.LastVerifiedAtUtc = now.ToUniversalTime();
        }
        return result;
    }

    public bool MatchesWorkOrder(WorkOrderRegistrationRequest request, Guid operation) =>
        ResourceType == "WorkOrder" && SourceService == "Maintenance" && SourceOperationId == operation &&
        EquipmentId == request.EquipmentId && ResourceId == request.OccurrenceId &&
        RequestPayloadJson == request.CanonicalJson() &&
        RequestHash is { } hash && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(hash, request.Hash());

    public bool ConfirmWorkOrder(WorkOrderRegistrationRequest request, Guid operation, Guid registrationId,
        long fence, long sourceVersion, DateTimeOffset now)
    {
        if (!MatchesWorkOrder(request, operation) || Id != registrationId || sourceVersion < 1 || State == "Aborted")
            throw new InvalidOperationException("Committed source proof does not match registration.");
        if (State is "Active" or "Released" && sourceVersion <= SourceVersion) return false;
        if (State is not ("Pending" or "Active"))
            throw new InvalidOperationException("Registration cannot be confirmed.");
        if (fence < FencingToken) throw new StaleWorkOrderFenceException();
        FencingToken = fence;
        SourceVersion = sourceVersion;
        State = "Active";
        ConfirmedAtUtc ??= now.ToUniversalTime();
        LastVerifiedAtUtc = now.ToUniversalTime();
        return true;
    }

    public bool AbortWorkOrder(WorkOrderRegistrationRequest request, Guid operation, long fence,
        Guid cancelIntent, DateTimeOffset now)
    {
        if (!MatchesWorkOrder(request, operation) || cancelIntent == Guid.Empty)
            throw new InvalidOperationException("Cancellation source proof does not match registration.");
        if (State == "Aborted")
        {
            if (CancelIntentId != cancelIntent) throw new InvalidOperationException("Abort intent mismatch.");
            return false;
        }
        if (State != "Pending") throw new InvalidOperationException("Committed registration cannot be aborted.");
        if (fence <= FencingToken) throw new StaleWorkOrderFenceException();
        FencingToken = fence;
        State = "Aborted";
        CancelIntentId = cancelIntent;
        LastVerifiedAtUtc = now.ToUniversalTime();
        return true;
    }

    public bool ObserveWorkOrderFence(long fence, DateTimeOffset now)
    {
        if (ResourceType != "WorkOrder" || RequestHash is null || fence < FencingToken ||
            State is not ("Pending" or "Active"))
            throw new InvalidOperationException("A current work-order registration fence is required.");
        if (fence == FencingToken) return false;
        FencingToken = fence;
        LastVerifiedAtUtc = now.ToUniversalTime();
        return true;
    }

    public bool ApplyWorkOrderVersion(WorkOrderRegistrationRequest payload, Guid operation, long fence, long version,
        string status, DateTimeOffset now)
    {
        if (!MatchesWorkOrder(payload, operation) || status is not ("Scheduled" or "InProgress" or "Completed" or "Closed" or "Cancelled"))
            throw new InvalidOperationException("Invalid WorkOrder event binding.");
        if (version <= SourceVersion || State is "Released" or "Aborted") return false;
        ConfirmWorkOrder(payload, operation, Id, fence, version, now);
        if (status is "Closed" or "Cancelled") { State = "Released"; ReleasedAtUtc = now; }
        return true;
    }

    private byte[]? requestHash;
    public byte[]? RequestHash { get => requestHash?.ToArray(); private set => requestHash = value?.ToArray(); }
    public string? RequestPayloadJson { get; private set; }
    public long FencingToken { get; private set; }
    public Guid? CancelIntentId { get; private set; }
    public string? EquipmentCodeSnapshot { get; private set; }
    public string? EquipmentNameSnapshot { get; private set; }
    public Guid? DepartmentIdSnapshot { get; private set; }
    public string? DepartmentNameSnapshot { get; private set; }

    public static EquipmentUseRegistration Register(Guid equipmentId, string source, string resourceType,
        Guid resourceId, Guid operationId, DateTimeOffset now) => new()
    {
        EquipmentId = equipmentId,
        SourceService = source,
        ResourceType = resourceType,
        ResourceId = resourceId,
        SourceOperationId = operationId,
        State = "Pending",
        RegisteredAtUtc = now
    };

    public bool ApplyIncidentVersion(long version, bool closed, DateTimeOffset now)
    {
        if (version <= SourceVersion || State is "Released" or "Aborted") return false;
        SourceVersion = version;
        ConfirmedAtUtc ??= now;
        State = closed ? "Released" : "Active";
        if (closed) ReleasedAtUtc = now;
        LastVerifiedAtUtc = now;
        return true;
    }

    public bool ApplyScheduleVersion(long version, bool releaseConfirmed, DateTimeOffset now)
    {
        if (ResourceType != "MaintenanceSchedule" || SourceService != "Maintenance") throw new InvalidOperationException("Schedule binding required.");
        if (version <= SourceVersion || State is "Released" or "Aborted") return false;
        SourceVersion = version; ConfirmedAtUtc ??= now; LastVerifiedAtUtc = now;
        State = releaseConfirmed ? "Released" : "Active";
        if (releaseConfirmed) ReleasedAtUtc = now;
        return true;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EquipmentId { get; private set; }
    public string SourceService { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public Guid SourceOperationId { get; private set; }
    public string State { get; private set; } = string.Empty;
    public long SourceVersion { get; private set; }
    public DateTimeOffset RegisteredAtUtc { get; private set; }
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public DateTimeOffset? ReleasedAtUtc { get; private set; }
    public DateTimeOffset? LastVerifiedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
