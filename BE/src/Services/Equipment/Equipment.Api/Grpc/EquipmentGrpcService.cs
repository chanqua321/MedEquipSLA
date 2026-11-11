using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Application.Features.Lookup;
using Microsoft.AspNetCore.Authorization;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
namespace MedEquipSLA.Equipment.Api.Grpc;
public sealed class EquipmentGrpcService(IWebHostEnvironment environment, EquipmentLookupQuery query,
    MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.UserResourceRegistrationHandler userRegistrations, IConfiguration configuration,
    MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.WorkOrderRegistrationHandler registrationHandler,
    MedEquipSLA.Equipment.Application.Features.Retirement.IRetirementStore retirement,
    MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.IResourceRegistrationStore resourceRegistrations)
    : EquipmentLookup.EquipmentLookupBase
{
    [Authorize(Policy = Authentication.RetirementSourceAuthentication.Policy)]
    public override async Task<ResourceUseReply> AbortResourceUse(MedEquipSLA.Contracts.Grpc.Equipment.V1.ResourceUseAbortRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.EquipmentId, out var equipment) || !Guid.TryParse(request.OperationId, out var operation) ||
            !Guid.TryParse(request.CancelIntentId, out var cancel) || cancel == Guid.Empty || request.FencingToken <= 0 ||
            request.CorrelationId.Length > 100)
            throw new RpcException(new(StatusCode.InvalidArgument, "Invalid resource abort binding."));
        var source = context.GetHttpContext().User.FindFirst("service")!.Value;
        try
        {
            var payload = new MedEquipSLA.Equipment.Domain.ResourceUseAbortRequest(equipment, operation, source,
                request.ResourceType, request.RequestHash.ToByteArray());
            var result = await resourceRegistrations.AbortAsync(payload, cancel, request.FencingToken,
                request.CorrelationId, context.CancellationToken);
            return result is null ? throw new RpcException(new(StatusCode.NotFound, "Equipment not found.")) : ResourceReply(result);
        }
        catch (InvalidOperationException) { throw new RpcException(new(StatusCode.FailedPrecondition, "Resource abort rejected.")); }
    }

    [Authorize(Policy = Authentication.RetirementSourceAuthentication.Policy)]
    public override async Task<ResourceUseReply> ReleaseScheduleUse(ScheduleReleaseRequest request, ServerCallContext context)
    {
        if (context.GetHttpContext().User.FindFirst("service")!.Value != "Maintenance")
            throw new RpcException(new(StatusCode.PermissionDenied, "Maintenance source required."));
        if (!Guid.TryParse(request.EquipmentId, out var equipment) || !Guid.TryParse(request.ScheduleId, out var schedule) ||
            !Guid.TryParse(request.RegistrationId, out var registration) || request.SourceVersion < 1 || request.CorrelationId.Length > 100)
            throw new RpcException(new(StatusCode.InvalidArgument, "Invalid schedule release binding."));
        try
        {
            var result = await resourceRegistrations.ReleaseScheduleAsync(equipment, schedule, registration,
                request.SourceVersion, request.CorrelationId, context.CancellationToken);
            return result is null ? throw new RpcException(new(StatusCode.NotFound, "Registration not found.")) : ResourceReply(result);
        }
        catch (InvalidOperationException) { throw new RpcException(new(StatusCode.FailedPrecondition, "Schedule release rejected.")); }
    }
    private static ResourceUseReply ResourceReply(MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.ResourceUseProof proof) =>
        new() { RegistrationId = proof.RegistrationId.ToString(), EquipmentId = proof.EquipmentId.ToString(),
            OperationId = proof.OperationId.ToString(), State = proof.State, FencingToken = proof.Fence,
            CancelIntentId = proof.CancelIntentId?.ToString() ?? "", RequestHash = Google.Protobuf.ByteString.CopyFrom(proof.RequestHash ?? []),
            SourceVersion = proof.SourceVersion };
    [Authorize(Policy = Authentication.RetirementSourceAuthentication.Policy)]
    public override async Task<RetirementListReply> ListRetirementIntents(RetirementListRequest request, ServerCallContext context)
    {
        if (request.Page is < 1 or > 1_000_000)
            throw new RpcException(new(StatusCode.InvalidArgument, "A valid page is required."));
        var reply = new RetirementListReply();
        reply.Intents.AddRange((await retirement.ListAsync(request.Page, context.CancellationToken)).Select(RetirementReply));
        return reply;
    }

    [Authorize(Policy = Authentication.RetirementSourceAuthentication.Policy)]
    public override async Task<RetirementIntentReply> GetRetirementIntent(RetirementIntentQuery request, ServerCallContext context)
    {
        var (equipment, intent) = RetirementBinding(request.EquipmentId, request.IntentId);
        var value = await retirement.FindAsync(equipment, intent, context.CancellationToken);
        return value is null ? throw new RpcException(new(StatusCode.NotFound, "Retirement intent not found.")) : RetirementReply(value);
    }

    [Authorize(Policy = Authentication.RetirementSourceAuthentication.Policy)]
    public override async Task<RetirementIntentReply> ReportRetirementSourceState(RetirementSourceReport request, ServerCallContext context)
    {
        var (equipment, intent) = RetirementBinding(request.EquipmentId, request.IntentId);
        Guid? proof = null;
        if (!string.IsNullOrEmpty(request.ProofId))
        {
            if (!Guid.TryParse(request.ProofId, out var parsed) || parsed == Guid.Empty)
                throw new RpcException(new(StatusCode.InvalidArgument, "Invalid source proof."));
            proof = parsed;
        }
        // Source comes exclusively from authenticated transport identity, never from a client-selected report field.
        var source = context.GetHttpContext().User.FindFirst("service")!.Value;
        var result = await retirement.ReportAsync(source, equipment, intent, request.RetirementEpoch, request.CoverageVersion,
            request.SourceGateVersion, request.SourceState, proof, request.CorrelationId, context.CancellationToken);
        return result.Value is { } value ? RetirementReply(value) :
            throw new RpcException(new(result.Outcome == MedEquipSLA.Equipment.Application.Features.Retirement.RetirementOutcome.NotFound
                ? StatusCode.NotFound : StatusCode.FailedPrecondition, "Source report could not be accepted."));
    }

    private static (Guid Equipment, Guid Intent) RetirementBinding(string equipment, string intent)
    {
        if (!Guid.TryParse(equipment, out var e) || e == Guid.Empty || !Guid.TryParse(intent, out var i) || i == Guid.Empty)
            throw new RpcException(new(StatusCode.InvalidArgument, "Equipment and intent identities are required."));
        return (e, i);
    }
    private static RetirementIntentReply RetirementReply(MedEquipSLA.Equipment.Application.Features.Retirement.RetirementView view) =>
        new() { EquipmentId = view.EquipmentId.ToString(), IntentId = view.Id.ToString(), RetirementEpoch = view.Epoch,
            State = view.State, ContinueCount = view.ContinueCount };
    [Authorize(Policy = Authentication.EquipmentServiceAuthentication.WorkOrderPolicy)]
    public override async Task<WorkOrderRegistrationReply> RegisterWorkOrder(
        MedEquipSLA.Contracts.Grpc.Equipment.V1.WorkOrderRegistrationRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EquipmentId, out var equipment) || !Guid.TryParse(request.ScheduleId, out var schedule) ||
            !Guid.TryParse(request.OccurrenceId, out var occurrence) || !Guid.TryParse(request.WorkOrderId, out var workOrder) ||
            !Guid.TryParse(request.OperationId, out var operation) || request.ScheduledForUtc is null || request.DueAtUtc is null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid work order identities and timestamps are required."));
        MedEquipSLA.Equipment.Domain.WorkOrderRegistrationRequest payload;
        try
        {
            payload = new(equipment, schedule, occurrence, workOrder, request.WorkType,
                request.ScheduledForUtc.ToDateTimeOffset(), request.DueAtUtc.ToDateTimeOffset());
        }
        catch (InvalidOperationException)
        { throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid timestamp.")); }
        return ProtocolReply(await registrationHandler.RegisterAsync(payload, operation, request.FencingToken,
            request.RequestHash.ToByteArray(), request.CorrelationId, context.CancellationToken));
    }

    [Authorize(Policy = Authentication.EquipmentServiceAuthentication.WorkOrderPolicy)]
    public override async Task<WorkOrderRegistrationReply> GetWorkOrderRegistrationByOperation(
        WorkOrderRegistrationQuery request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EquipmentId, out var equipment) || !Guid.TryParse(request.OccurrenceId, out var occurrence) ||
            !Guid.TryParse(request.OperationId, out var operation))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid identities are required."));
        return ProtocolReply(await registrationHandler.FindAsync(equipment, occurrence, operation,
            request.RequestHash.ToByteArray(), context.CancellationToken));
    }

    [Authorize(Policy = Authentication.EquipmentServiceAuthentication.WorkOrderPolicy)]
    public override async Task<WorkOrderRegistrationReply> ConfirmWorkOrderCommitted(
        WorkOrderCommitRequest request, ServerCallContext context)
    {
        var r = request.Registration;
        if (r is null || !Guid.TryParse(request.RegistrationId, out var registration))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Registration proof is required."));
        var (payload, operation) = ParseProtocolPayload(r);
        return ProtocolReply(await registrationHandler.ConfirmAsync(payload, operation, registration,
            r.FencingToken, request.SourceVersion, r.RequestHash.ToByteArray(), r.CorrelationId, context.CancellationToken));
    }

    [Authorize(Policy = Authentication.EquipmentServiceAuthentication.WorkOrderPolicy)]
    public override async Task<WorkOrderRegistrationReply> VerifyAndAbortWorkOrderRegistration(
        WorkOrderAbortRequest request, ServerCallContext context)
    {
        var r = request.Registration;
        if (r is null || !Guid.TryParse(request.CancelIntentId, out var cancel))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Durable cancellation proof is required."));
        var (payload, operation) = ParseProtocolPayload(r);
        return ProtocolReply(await registrationHandler.AbortAsync(payload, operation,
            r.FencingToken, cancel, r.RequestHash.ToByteArray(), r.CorrelationId, context.CancellationToken));
    }

    private static (MedEquipSLA.Equipment.Domain.WorkOrderRegistrationRequest, Guid) ParseProtocolPayload(
        MedEquipSLA.Contracts.Grpc.Equipment.V1.WorkOrderRegistrationRequest r)
    {
        if (!Guid.TryParse(r.EquipmentId, out var equipment) || !Guid.TryParse(r.ScheduleId, out var schedule) ||
            !Guid.TryParse(r.OccurrenceId, out var occurrence) || !Guid.TryParse(r.WorkOrderId, out var workOrder) ||
            !Guid.TryParse(r.OperationId, out var operation) || r.ScheduledForUtc is null || r.DueAtUtc is null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid registration identities are required."));
        try { return (new(equipment, schedule, occurrence, workOrder, r.WorkType,
            r.ScheduledForUtc.ToDateTimeOffset(), r.DueAtUtc.ToDateTimeOffset()), operation); }
        catch (InvalidOperationException)
        { throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid timestamp.")); }
    }

    private static WorkOrderRegistrationReply ProtocolReply(
        MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.RegistrationResult result)
    {
        // Map domain/store outcomes without exposing SQL or credential details.
        if (result.Outcome == MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.RegistrationOutcome.Absent)
            return new() { Found = false };
        if (result.Registration is { } r)
            return new() { Found = true, RegistrationId = r.Id.ToString(), EquipmentId = r.EquipmentId.ToString(),
                OccurrenceId = r.OccurrenceId.ToString(), OperationId = r.OperationId.ToString(), State = r.State,
                FencingToken = r.Fence, RequestHash = Google.Protobuf.ByteString.CopyFrom(r.Hash),
                EquipmentCode = r.EquipmentCode, EquipmentName = r.EquipmentName, DepartmentId = r.DepartmentId.ToString(),
                DepartmentName = r.DepartmentName, SourceVersion = r.SourceVersion, CancelIntentId = r.CancelIntentId?.ToString() ?? "" };
        var code = result.Outcome switch
        {
            MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.RegistrationOutcome.Invalid => StatusCode.InvalidArgument,
            MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.RegistrationOutcome.NotFound => StatusCode.NotFound,
            MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.RegistrationOutcome.StaleFence => StatusCode.Aborted,
            MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.RegistrationOutcome.Conflict => StatusCode.AlreadyExists,
            _ => StatusCode.FailedPrecondition
        };
        throw new RpcException(new Status(code, result.Outcome.ToString()));
    }

    [AllowAnonymous]
    public override Task<ServiceInfoReply> GetServiceInfo(ServiceInfoRequest request, ServerCallContext context)
    {
        if (!environment.IsDevelopment()) throw new RpcException(new Status(StatusCode.PermissionDenied, "Development probe only"));
        return Task.FromResult(new ServiceInfoReply { Service = "Equipment", Phase = "ready" });
    }
    [Authorize(Roles = "Admin,DepartmentStaff")]
    public override Task<RegisterIncidentUseReply> RegisterIncidentUse(RegisterIncidentUseRequest request, ServerCallContext context) =>
        RegisterUseAsync(request, context, "Incident", "Incident", "x-incident-registration-key");

    [Authorize(Roles = "Admin")]
    public override Task<RegisterIncidentUseReply> RegisterScheduleUse(RegisterIncidentUseRequest request, ServerCallContext context) =>
        RegisterUseAsync(request, context, "Maintenance", "MaintenanceSchedule", "x-maintenance-registration-key");

    private async Task<RegisterIncidentUseReply> RegisterUseAsync(RegisterIncidentUseRequest request, ServerCallContext context,
        string source, string resourceType, string keyHeader)
    {
        var secret = configuration[$"Grpc:{source}RegistrationKey"];
        var supplied = context.RequestHeaders.GetValue(keyHeader);
        if (string.IsNullOrWhiteSpace(secret) || supplied is null ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(supplied)))
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Source service credentials are required."));

        if (!Guid.TryParse(request.EquipmentId, out var id) || id == Guid.Empty ||
            !Guid.TryParse(request.OperationId, out var operation) || operation == Guid.Empty)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Equipment and operation IDs are required."));
        if (!EquipmentAccess.TryScope(context.GetHttpContext().User, out var department))
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Equipment access is required."));

        var result = await userRegistrations.HandleAsync(id, operation, source, resourceType, department, context.CancellationToken);
        if (result.Value is not { } value)
            throw new RpcException(new Status(result.Outcome == MedEquipSLA.Equipment.Application.Features.WorkOrderRegistration.RegistrationOutcome.NotFound
                ? StatusCode.NotFound : StatusCode.FailedPrecondition, "Registration does not allow creation."));
        return new() { RegistrationId = value.RegistrationId.ToString(), EquipmentCode = value.EquipmentCode, EquipmentName = value.EquipmentName,
            DepartmentId = value.DepartmentId.ToString(), DepartmentName = value.DepartmentName };
    }

    [Authorize]
    public override async Task<EquipmentReply> GetEquipment(GetEquipmentRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EquipmentId, out var id) || id == Guid.Empty)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Equipment ID must be a UUID."));
        if (!EquipmentAccess.TryScope(context.GetHttpContext().User, out var department))
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Equipment access is required."));
        var asset = await query.Handle(id, department, context.CancellationToken);
        if (asset is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Equipment not found."));
        return new()
        {
            EquipmentId = asset.Id.ToString(), AssetCode = asset.Code, DepartmentId = asset.DepartmentId.ToString(),
            Status = asset.Status, AggregateVersion = asset.Version
        };
    }
}
