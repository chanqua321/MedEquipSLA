using Google.Protobuf;
using Grpc.Core;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Host = MedEquipSLA.Database.IntegrationTests.EquipmentWorkOrderGrpcTests.ProtocolHost;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class EquipmentWorkOrderAbortGrpcTests
{
    [SqlServerFact]
    public async Task Abort_before_register_is_durable_idempotent_and_rejects_late_register_and_confirm()
    {
        await using var host = await Host.CreateAsync();
        var payload = host.Request();
        payload.FencingToken = 2;
        var cancelId = Guid.NewGuid().ToString();
        var abort = new WorkOrderAbortRequest { Registration = payload, CancelIntentId = cancelId };
        var first = await host.Client.VerifyAndAbortWorkOrderRegistrationAsync(abort, host.Credentials);
        Assert.Equal("Aborted", first.State);
        Assert.Equal(cancelId, first.CancelIntentId);
        Assert.Equal(first, await host.Client.VerifyAndAbortWorkOrderRegistrationAsync(abort, host.Credentials));
        var late = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.RegisterWorkOrderAsync(payload, host.Credentials));
        Assert.Equal(StatusCode.FailedPrecondition, late.StatusCode);
        var lateConfirm = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.ConfirmWorkOrderCommittedAsync(new()
            { Registration = payload, RegistrationId = first.RegistrationId, SourceVersion = 1 }, host.Credentials));
        Assert.Equal(StatusCode.FailedPrecondition, lateConfirm.StatusCode);
        var found = await host.Client.GetWorkOrderRegistrationByOperationAsync(new()
        {
            EquipmentId = payload.EquipmentId, OccurrenceId = payload.OccurrenceId,
            OperationId = payload.OperationId, RequestHash = payload.RequestHash
        }, host.Credentials);
        Assert.Equal(first, found);
        var wrongIntent = abort.Clone(); wrongIntent.CancelIntentId = Guid.NewGuid().ToString();
        var mismatch = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.VerifyAndAbortWorkOrderRegistrationAsync(wrongIntent, host.Credentials));
        Assert.Equal(StatusCode.AlreadyExists, mismatch.StatusCode);
        await using var db = host.Context();
        Assert.Equal(1, await db.EquipmentUseRegistrations.CountAsync());
        Assert.Equal(1, await db.AuditEntries.CountAsync());
    }

    [SqlServerFact]
    public async Task Pending_abort_requires_new_fence_and_does_not_confuse_source_version()
    {
        await using var host = await Host.CreateAsync();
        var payload = host.Request();
        var registered = await host.Client.RegisterWorkOrderAsync(payload, host.Credentials);
        var abort = new WorkOrderAbortRequest { Registration = payload.Clone(), CancelIntentId = Guid.NewGuid().ToString() };
        var stale = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.VerifyAndAbortWorkOrderRegistrationAsync(abort, host.Credentials));
        Assert.Equal(StatusCode.Aborted, stale.StatusCode);
        abort.Registration.FencingToken = 2;
        var result = await host.Client.VerifyAndAbortWorkOrderRegistrationAsync(abort, host.Credentials);
        Assert.Equal(registered.RegistrationId, result.RegistrationId);
        Assert.Equal("Aborted", result.State);
        Assert.Equal(2, result.FencingToken);
        Assert.Equal(0, result.SourceVersion);
        Assert.Equal(result, await host.Client.VerifyAndAbortWorkOrderRegistrationAsync(abort, host.Credentials));
        await using var db = host.Context();
        Assert.Equal(2, await db.AuditEntries.CountAsync());
    }

    [SqlServerFact]
    public async Task Confirmation_is_idempotent_and_committed_registration_cannot_be_aborted()
    {
        await using var host = await Host.CreateAsync();
        var payload = host.Request();
        var registered = await host.Client.RegisterWorkOrderAsync(payload, host.Credentials);
        var confirm = new WorkOrderCommitRequest
        { Registration = payload, RegistrationId = registered.RegistrationId, SourceVersion = 1 };
        var first = await host.Client.ConfirmWorkOrderCommittedAsync(confirm, host.Credentials);
        Assert.Equal("Active", first.State);
        Assert.Equal(1, first.SourceVersion);
        Assert.Equal(1, first.FencingToken);
        Assert.Equal(first, await host.Client.ConfirmWorkOrderCommittedAsync(confirm, host.Credentials));
        var cancellation = payload.Clone(); cancellation.FencingToken = 2;
        var denied = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.VerifyAndAbortWorkOrderRegistrationAsync(new()
            { Registration = cancellation, CancelIntentId = Guid.NewGuid().ToString() }, host.Credentials));
        Assert.Equal(StatusCode.FailedPrecondition, denied.StatusCode);
        var unauthenticated = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.ConfirmWorkOrderCommittedAsync(confirm));
        Assert.Equal(StatusCode.Unauthenticated, unauthenticated.StatusCode);
        await using var db = host.Context();
        Assert.Equal("Active", (await db.EquipmentUseRegistrations.SingleAsync()).State);
        Assert.Equal(2, await db.AuditEntries.CountAsync());
    }

    [SqlServerFact]
    public async Task Local_commit_failure_rolls_back_confirmation_and_retry_reuses_registration()
    {
        await using var host = await Host.CreateAsync();
        var payload = host.Request();
        var registered = await host.Client.RegisterWorkOrderAsync(payload, host.Credentials);
        var confirm = new WorkOrderCommitRequest
        { Registration = payload, RegistrationId = registered.RegistrationId, SourceVersion = 1 };
        await using (var db = host.Context())
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE dbo.AuditEntries ADD CONSTRAINT TestConfirmRollback CHECK (Action <> N'WorkOrderRegistrationCommitted')");
        await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.ConfirmWorkOrderCommittedAsync(confirm, host.Credentials));
        var query = new WorkOrderRegistrationQuery
        {
            EquipmentId = payload.EquipmentId, OccurrenceId = payload.OccurrenceId,
            OperationId = payload.OperationId, RequestHash = payload.RequestHash
        };
        var pending = await host.Client.GetWorkOrderRegistrationByOperationAsync(query, host.Credentials);
        Assert.Equal("Pending", pending.State);
        Assert.Equal(0, pending.SourceVersion);
        await using (var db = host.Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.AuditEntries DROP CONSTRAINT TestConfirmRollback");
        var recovered = await host.Client.ConfirmWorkOrderCommittedAsync(confirm, host.Credentials);
        Assert.Equal(registered.RegistrationId, recovered.RegistrationId);
        Assert.Equal("Active", recovered.State);
        Assert.Equal(recovered, await host.Client.GetWorkOrderRegistrationByOperationAsync(query, host.Credentials));
        var mismatch = confirm.Clone(); mismatch.Registration.RequestHash = ByteString.CopyFrom(new byte[32]);
        var invalid = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.ConfirmWorkOrderCommittedAsync(mismatch, host.Credentials));
        Assert.Equal(StatusCode.AlreadyExists, invalid.StatusCode);
    }
}
