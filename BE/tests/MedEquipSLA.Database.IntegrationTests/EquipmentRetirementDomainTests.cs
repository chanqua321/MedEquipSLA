using System.Reflection;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;
using Xunit;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class EquipmentRetirementDomainTests
{
    internal static readonly DateTimeOffset Now = new(2026, 11, 6, 0, 0, 0, TimeSpan.Zero);
    internal static readonly Guid Actor = Guid.NewGuid();
    internal static EquipmentAsset Asset() => EquipmentAsset.Register("EQ-" + Guid.NewGuid().ToString("N"),
        Guid.NewGuid(), new("Device", null, Guid.NewGuid(), null, null, null, null), Actor, Now);
    internal static EquipmentRetirementIntent Begin(EquipmentAsset asset,
        params EquipmentRetirementIntent[] history) =>
        EquipmentRetirementIntent.Begin(asset, history, Actor, Guid.NewGuid(), "Retirement requested", Now);
    internal static void Report(EquipmentRetirementIntent intent, string source, string state, long version = 1) =>
        intent.RecordSource(source, source, intent.EquipmentId, intent.Id, intent.RetirementEpoch, 1, version,
            state, state is "Sealed" or "CancelAck" ? Guid.NewGuid() : null, Now);
    internal static void Seal(EquipmentRetirementIntent intent)
    { Report(intent, "Maintenance", "Sealed"); Report(intent, "Incident", "Sealed"); }

    [Fact]
    public void Missing_or_blocked_sources_never_authorize_retirement_and_no_timeout_transition()
    {
        var asset = Asset();
        asset.ApplyBlockingStatus(true, false, Now);
        var intent = Begin(asset);
        Assert.Equal("Coordinating", intent.State);
        Assert.Throws<InvalidOperationException>(() => VerifiedRetirementEligibility.Create(intent, true, false, false, false));
        Report(intent, "Maintenance", "Sealed");
        Report(intent, "Incident", "Blocked");
        Assert.Equal("Coordinating", intent.State);
        Assert.Throws<InvalidOperationException>(() => VerifiedRetirementEligibility.Create(intent, true, false, false, false));
        intent.Continue(Actor, "Check again", Now.AddYears(1));
        Assert.Equal("Broken", asset.OperationalStatus);
        Assert.Equal("Coordinating", intent.State);
        Assert.Null(intent.CompletedAtUtc);
    }

    [Fact]
    public void Continue_and_verified_completion_preserve_identity_epoch_and_history()
    {
        var asset = Asset();
        var intent = Begin(asset);
        var id = intent.Id;
        intent.Continue(Actor, "Retry coordination", Now);
        Assert.Equal(id, intent.Id);
        Assert.Equal(1, intent.RetirementEpoch);
        Assert.Equal(1, intent.ContinueCount);
        Seal(intent);
        Assert.Equal("Ready", intent.State);
        Assert.Throws<InvalidOperationException>(() => VerifiedRetirementEligibility.Create(intent, true, true, false, false));
        Assert.Throws<InvalidOperationException>(() => VerifiedRetirementEligibility.Create(intent, true, false, false, true));
        var proof = VerifiedRetirementEligibility.Create(intent, true, false, false, false);
        asset.Retire(intent, proof, Now);
        var history = EquipmentStatusHistory.Retirement(asset.Id, "Operational", intent, Now);
        Assert.True(asset.IsRetired);
        Assert.Equal("Retired", asset.OperationalStatus);
        Assert.Equal("Completed", intent.State);
        Assert.Equal(Actor, history.ActorUserId);
        Assert.Equal(intent.Reason, history.Reason);
        Assert.Equal("Operational", history.FromStatus);
        Assert.Equal("Retired", history.ToStatus);
        Assert.Null(history.SourceEventId);
        Assert.Throws<InvalidOperationException>(() => intent.Continue(Actor, "Cannot retry", Now));
        Assert.Throws<InvalidOperationException>(() => intent.RequestCancel(Actor, "Too late", Now));
        Assert.Throws<InvalidOperationException>(() => asset.Retire(intent, proof, Now));
        Assert.False(asset.ApplyBlockingStatus(false, false, Now));
        Assert.Equal("Retired", asset.OperationalStatus);
        Assert.Null(typeof(EquipmentAsset).GetMethod("Retire", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void Active_history_prevents_second_intent_and_cancel_requires_all_acks()
    {
        var asset = Asset();
        var first = Begin(asset);
        Assert.Throws<InvalidOperationException>(() => Begin(asset, first));
        Assert.Throws<InvalidOperationException>(() => first.ConfirmCancelled(Now));
        first.RequestCancel(Actor, "Keep equipment", Now);
        first.Continue(Actor, "Retry pending cancellation acknowledgments", Now);
        Assert.Equal("Cancelling", first.State);
        Report(first, "Maintenance", "CancelAck");
        Assert.Throws<InvalidOperationException>(() => first.ConfirmCancelled(Now));
        Report(first, "Incident", "CancelAck");
        first.ConfirmCancelled(Now);
        Assert.Equal("Cancelled", first.State);
        Assert.Equal(1, first.RetirementEpoch);
        Assert.Throws<InvalidOperationException>(() => first.Continue(Actor, "Retry terminal", Now));
        Assert.Throws<InvalidOperationException>(() => first.RequestCancel(Actor, "Repeat terminal", Now));
        Assert.Throws<InvalidOperationException>(() => first.RecordSource("Maintenance", "Maintenance",
            asset.Id, first.Id, 1, 1, 2, "Frozen", null, Now));
        var second = Begin(asset, first);
        Assert.Equal(2, second.RetirementEpoch);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("Operational", asset.OperationalStatus);
    }

    [Fact]
    public void Source_binding_coverage_epoch_duplicate_and_stale_proof_are_checked()
    {
        var asset = Asset();
        var intent = Begin(asset);
        Assert.Throws<InvalidOperationException>(() => intent.RecordSource("Maintenance", "Incident",
            asset.Id, intent.Id, 1, 1, 1, "Sealed", Guid.NewGuid(), Now));
        Assert.Throws<InvalidOperationException>(() => intent.RecordSource("Maintenance", "Maintenance",
            asset.Id, intent.Id, 2, 1, 1, "Frozen", null, Now));
        Assert.Throws<InvalidOperationException>(() => intent.RecordSource("Maintenance", "Maintenance",
            Guid.NewGuid(), intent.Id, 1, 1, 1, "Frozen", null, Now));
        Assert.Throws<InvalidOperationException>(() => intent.RecordSource("Maintenance", "Maintenance",
            asset.Id, intent.Id, 1, 2, 1, "Sealed", Guid.NewGuid(), Now));
        Assert.Throws<InvalidOperationException>(() => intent.RecordSource("Maintenance", "Maintenance",
            asset.Id, intent.Id, 1, 1, 1, "Sealed", null, Now));
        var proof = Guid.NewGuid();
        Assert.True(intent.RecordSource("Maintenance", "Maintenance", asset.Id, intent.Id, 1, 1, 2, "Sealed", proof, Now));
        Assert.False(intent.RecordSource("Maintenance", "Maintenance", asset.Id, intent.Id, 1, 1, 2, "Sealed", proof, Now));
        Assert.Throws<InvalidOperationException>(() => intent.RecordSource("Maintenance", "Maintenance",
            asset.Id, intent.Id, 1, 1, 1, "Frozen", null, Now));
        Assert.Throws<InvalidOperationException>(() => intent.RecordSource("Maintenance", "Maintenance",
            asset.Id, intent.Id, 1, 1, 3, "Sealed", Guid.NewGuid(), Now));
        Assert.Throws<InvalidOperationException>(() => intent.RecordSource("Incident", "Incident",
            asset.Id, intent.Id, 1, 1, 1, "CancelAck", Guid.NewGuid(), Now));
    }

    [Fact]
    public void Canonical_workorder_snapshot_is_immutable_and_does_not_change_broken_status()
    {
        var asset = Asset();
        asset.ApplyBlockingStatus(true, false, Now);
        var request = new WorkOrderRegistrationRequest(asset.Id, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Maintenance", Now, Now.AddHours(2));
        var op = Guid.NewGuid();
        var registration = EquipmentUseRegistration.RegisterWorkOrder(asset, request, op, 1, "Initial department", Now);
        var hash = registration.RequestHash!;
        Assert.Equal(32, hash.Length);
        hash[0] ^= 0xff;
        Assert.Equal(request.Hash(), registration.RequestHash);
        var offsetRequest = request with { ScheduledForUtc = Now.ToOffset(TimeSpan.FromHours(7)),
            DueAtUtc = Now.AddHours(2).ToOffset(TimeSpan.FromHours(7)) };
        Assert.Equal(request.CanonicalJson(), offsetRequest.CanonicalJson());
        asset.UpdateProfile(new("Renamed", null, asset.CategoryId, null, null, null, null), Actor, Now);
        asset.Transfer(Guid.NewGuid(), Actor, Now);
        Assert.True(registration.MatchesWorkOrder(request, op));
        Assert.False(registration.MatchesWorkOrder(request with { WorkType = "Calibration" }, op));
        Assert.Equal("Device", registration.EquipmentNameSnapshot);
        Assert.Equal("Initial department", registration.DepartmentNameSnapshot);
        Assert.NotEqual(asset.DepartmentId, registration.DepartmentIdSnapshot);
        Assert.Equal("Broken", asset.OperationalStatus);
        Assert.Equal(0, registration.SourceVersion);
        Assert.Equal(1, registration.FencingToken);
        Assert.Equal("Pending", registration.State);
    }

    [Fact]
    public void Legacy_incident_and_schedule_semantics_and_audit_actor_types_are_preserved()
    {
        var incident = EquipmentUseRegistration.Register(Guid.NewGuid(), "Incident", "Incident", Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.True(incident.ApplyIncidentVersion(1, false, Now));
        Assert.Equal("Active", incident.State);
        Assert.False(incident.ApplyIncidentVersion(1, true, Now));
        Assert.True(incident.ApplyIncidentVersion(2, true, Now));
        Assert.Equal("Released", incident.State);
        Assert.False(incident.ApplyIncidentVersion(3, false, Now));
        var schedule = EquipmentUseRegistration.Register(Guid.NewGuid(), "Maintenance", "MaintenanceSchedule", Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.Equal("Pending", schedule.State);
        Assert.Null(schedule.RequestHash);
        Assert.Null(schedule.RequestPayloadJson);
        Assert.Equal(0, schedule.FencingToken);
        var admin = new AuditEntry(Actor, "Request", "Equipment", Guid.NewGuid(), Now, "correlation", "{}");
        var service = AuditEntry.ForService("Maintenance", "Report", "Receipt", Guid.NewGuid(), Now, "correlation", "{}");
        Assert.Equal(Actor, admin.ActorUserId);
        Assert.Null(admin.ActorService);
        Assert.Null(service.ActorUserId);
        Assert.Equal("Maintenance", service.ActorService);
        Assert.Throws<ArgumentException>(() => AuditEntry.ForService("secret-token", "Report", "Receipt", Guid.NewGuid(), Now, "correlation", "{}"));
    }
}
