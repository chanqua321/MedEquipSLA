using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
using MedEquipSLA.Equipment.Api.Authentication;
using MedEquipSLA.Equipment.Api.Grpc;
using MedEquipSLA.Equipment.Application.Features.Lookup;
using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure;
using MedEquipSLA.Equipment.Infrastructure.Persistence;
using MedEquipSLA.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using Payload = MedEquipSLA.Equipment.Domain.WorkOrderRegistrationRequest;
using WireRequest = MedEquipSLA.Contracts.Grpc.Equipment.V1.WorkOrderRegistrationRequest;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class EquipmentWorkOrderGrpcTests
{
    [SqlServerFact]
    public async Task Service_identity_is_required_and_cannot_replace_user_authorization()
    {
        await using var host = await ProtocolHost.CreateAsync();
        var request = host.Request();
        var missing = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.RegisterWorkOrderAsync(request));
        Assert.Equal(StatusCode.Unauthenticated, missing.StatusCode);
        var wrong = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.RegisterWorkOrderAsync(request, new Metadata { { EquipmentServiceAuthentication.Header, "wrong" } }));
        Assert.Equal(StatusCode.Unauthenticated, wrong.StatusCode);
        var jwtOnly = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.RegisterWorkOrderAsync(request, new Metadata { { "authorization", "Bearer " + host.AdminToken() } }));
        Assert.Equal(StatusCode.Unauthenticated, jwtOnly.StatusCode);
        var noUserJwt = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.GetEquipmentAsync(new() { EquipmentId = host.AssetId.ToString() }, host.Credentials));
        Assert.Equal(StatusCode.Unauthenticated, noUserJwt.StatusCode);
        var ok = await host.Client.RegisterWorkOrderAsync(request, host.Credentials);
        Assert.True(ok.Found);
        await using var db = host.Context();
        Assert.Null((await db.AuditEntries.SingleAsync()).ActorUserId);
        Assert.Equal("Maintenance", (await db.AuditEntries.SingleAsync()).ActorService);
    }

    [SqlServerFact]
    public async Task Concurrent_duplicate_requests_reuse_registration_hash_snapshot_and_one_audit()
    {
        await using var host = await ProtocolHost.CreateAsync();
        var request = host.Request();
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
            await host.Client.RegisterWorkOrderAsync(request, host.Credentials)));
        Assert.Single(results.Select(x => x.RegistrationId).Distinct());
        await using var db = host.Context();
        Assert.Equal(1, await db.EquipmentUseRegistrations.CountAsync());
        Assert.Equal(1, await db.AuditEntries.CountAsync());
        var first = results[0];
        var asset = await db.EquipmentAssets.SingleAsync();
        asset.UpdateProfile(new("Renamed device", null, asset.CategoryId, null, null, null, null),
            EquipmentRetirementDomainTests.Actor, DateTimeOffset.UtcNow);
        var unit = Department.Create("D-" + Guid.NewGuid().ToString("N"), "Moved", EquipmentRetirementDomainTests.Actor, DateTimeOffset.UtcNow);
        db.Add(unit);
        await db.SaveChangesAsync();
        asset.Transfer(unit.Id, EquipmentRetirementDomainTests.Actor, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        var replay = await host.Client.RegisterWorkOrderAsync(request, host.Credentials);
        Assert.Equal(first, replay);
        var found = await host.Client.GetWorkOrderRegistrationByOperationAsync(new()
        {
            EquipmentId = request.EquipmentId, OccurrenceId = request.OccurrenceId,
            OperationId = request.OperationId, RequestHash = request.RequestHash
        }, host.Credentials);
        Assert.Equal(replay, found);
        var changed = request.Clone();
        changed.WorkType = "Calibration";
        changed.RequestHash = ByteString.CopyFrom(ProtocolHost.PayloadOf(changed).Hash());
        var conflict = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.RegisterWorkOrderAsync(changed, host.Credentials));
        Assert.Equal(StatusCode.AlreadyExists, conflict.StatusCode);
        var resourceConflict = request.Clone();
        resourceConflict.OperationId = Guid.NewGuid().ToString();
        var duplicateResource = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.RegisterWorkOrderAsync(resourceConflict, host.Credentials));
        Assert.Equal(StatusCode.AlreadyExists, duplicateResource.StatusCode);
        var stale = request.Clone();
        stale.FencingToken = 2;
        var advanced = await host.Client.RegisterWorkOrderAsync(stale, host.Credentials);
        Assert.Equal(2, advanced.FencingToken);
        var staleResult = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.RegisterWorkOrderAsync(request, host.Credentials));
        Assert.Equal(StatusCode.Aborted, staleResult.StatusCode);
    }

    [SqlServerFact]
    public async Task Broken_is_allowed_and_retirement_intent_blocks_only_new_reservations()
    {
        await using var host = await ProtocolHost.CreateAsync();
        await using (var db = host.Context())
        {
            var asset = await db.EquipmentAssets.SingleAsync();
            asset.ApplyBlockingStatus(true, false, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        var request = host.Request();
        var registered = await host.Client.RegisterWorkOrderAsync(request, host.Credentials);
        await using (var db = host.Context())
        {
            var asset = await db.EquipmentAssets.SingleAsync();
            Assert.Equal("Broken", asset.OperationalStatus);
            db.Add(EquipmentRetirementDomainTests.Begin(asset));
            await db.SaveChangesAsync();
        }
        Assert.Equal(registered, await host.Client.RegisterWorkOrderAsync(request, host.Credentials));
        var blocked = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.RegisterWorkOrderAsync(host.Request(), host.Credentials));
        Assert.Equal(StatusCode.FailedPrecondition, blocked.StatusCode);
        Assert.Equal("AdmissionClosed", blocked.Status.Detail);
    }

    [SqlServerFact]
    public async Task Invalid_missing_retired_and_unknown_registration_results_are_distinct()
    {
        await using var host = await ProtocolHost.CreateAsync();
        var request = host.Request();
        var absent = await host.Client.GetWorkOrderRegistrationByOperationAsync(new()
        {
            EquipmentId = request.EquipmentId, OccurrenceId = request.OccurrenceId,
            OperationId = request.OperationId, RequestHash = request.RequestHash
        }, host.Credentials);
        Assert.False(absent.Found);
        var malformed = request.Clone();
        malformed.RequestHash = ByteString.Empty;
        var invalid = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.RegisterWorkOrderAsync(malformed, host.Credentials));
        Assert.Equal(StatusCode.InvalidArgument, invalid.StatusCode);
        var missing = request.Clone();
        missing.EquipmentId = Guid.NewGuid().ToString();
        missing.RequestHash = ByteString.CopyFrom(ProtocolHost.PayloadOf(missing).Hash());
        var notFound = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.RegisterWorkOrderAsync(missing, host.Credentials));
        Assert.Equal(StatusCode.NotFound, notFound.StatusCode);
        await using (var db = host.Context())
        {
            var asset = await db.EquipmentAssets.SingleAsync();
            var intent = EquipmentRetirementDomainTests.Begin(asset);
            EquipmentRetirementDomainTests.Seal(intent);
            asset.Retire(intent, VerifiedRetirementEligibility.Create(intent, true, false, false, false), DateTimeOffset.UtcNow);
            db.Add(intent);
            await db.SaveChangesAsync();
        }
        var retired = await Assert.ThrowsAsync<RpcException>(async () =>
            await host.Client.RegisterWorkOrderAsync(request, host.Credentials));
        Assert.Equal(StatusCode.FailedPrecondition, retired.StatusCode);
        Assert.Equal("Retired", retired.Status.Detail);
    }

    internal sealed class ProtocolHost : IAsyncDisposable
    {
        private readonly TestDatabase database;
        private readonly WebApplication app;
        private readonly GrpcChannel channel;
        private readonly RSA signingKey;
        private readonly string publicKeyFile;
        internal Guid AssetId { get; }
        internal EquipmentLookup.EquipmentLookupClient Client { get; }
        internal Metadata Credentials { get; }

        private ProtocolHost(TestDatabase database, WebApplication app, GrpcChannel channel, Guid asset,
            string credential, RSA rsa, string keyFile)
        {
            this.database = database; this.app = app; this.channel = channel;
            signingKey = rsa; publicKeyFile = keyFile; AssetId = asset;
            Client = new(channel); Credentials = new() { { EquipmentServiceAuthentication.Header, credential } };
        }

        internal EquipmentDbContext Context() => (EquipmentDbContext)Contexts.Create("Equipment", database.ConnectionString);
        internal string VerificationKeyFile => publicKeyFile;
        internal HttpClient Http() => new()
        {
            BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()),
            DefaultRequestVersion = HttpVersion.Version20, DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        internal Metadata RetirementCredentials(string source) => new()
        {
            { "x-retirement-source", source }, { "x-retirement-source-key", Credentials.GetValue(EquipmentServiceAuthentication.Header)! + "-" + source }
        };
        internal Metadata RegistrationCredentials(string source) => new()
        {
            { "authorization", "Bearer " + AdminToken() },
            { source == "Incident" ? "x-incident-registration-key" : "x-maintenance-registration-key",
                Credentials.GetValue(EquipmentServiceAuthentication.Header)! + "-" + source + "-registration" }
        };
        internal static Payload PayloadOf(WireRequest r) => new(Guid.Parse(r.EquipmentId), Guid.Parse(r.ScheduleId),
            Guid.Parse(r.OccurrenceId), Guid.Parse(r.WorkOrderId), r.WorkType, r.ScheduledForUtc.ToDateTimeOffset(), r.DueAtUtc.ToDateTimeOffset());
        internal WireRequest Request()
        {
            var now = EquipmentRetirementDomainTests.Now;
            var r = new WireRequest { EquipmentId = AssetId.ToString(), ScheduleId = Guid.NewGuid().ToString(),
                OccurrenceId = Guid.NewGuid().ToString(), WorkOrderId = Guid.NewGuid().ToString(), OperationId = Guid.NewGuid().ToString(),
                WorkType = "Maintenance", FencingToken = 1, ScheduledForUtc = Timestamp.FromDateTimeOffset(now),
                DueAtUtc = Timestamp.FromDateTimeOffset(now.AddHours(1)), CorrelationId = Guid.NewGuid().ToString() };
            r.RequestHash = ByteString.CopyFrom(PayloadOf(r).Hash());
            return r;
        }
        internal string AdminToken(string role = "Admin", Guid? subject = null, long authorizationVersion = 1) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "test-issuer", audience: "test-audience", claims:
            [new Claim(ClaimTypes.NameIdentifier, (subject ?? EquipmentRetirementDomainTests.Actor).ToString()), new Claim(ClaimTypes.Role, role),
             new Claim("authorization_version", authorizationVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)), new Claim("session_id", Guid.NewGuid().ToString())],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new RsaSecurityKey(signingKey), SecurityAlgorithms.RsaSha256)));

        internal static async Task<ProtocolHost> CreateAsync()
        {
            var database = await TestDatabase.CreateAsync("Equipment");
            var rsa = RSA.Create(2048);
            var keyFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(keyFile, rsa.ExportRSAPublicKeyPem());
            WebApplication? app = null;
            try
            {
                Guid asset;
                await using (var db = (EquipmentDbContext)Contexts.Create("Equipment", database.ConnectionString))
                { await db.Database.MigrateAsync(); asset = (await EquipmentRetirementSqlTests.SeedAsync(db)).Id; }
                var credential = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
                // Referencing the API copies its appsettings into test output. Never inherit app endpoints/connections.
                builder.Configuration.Sources.Clear();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Database"] = database.ConnectionString, ["Workers:Enabled"] = "false",
                    ["Grpc:MaintenanceWorkOrderKey"] = credential, ["Jwt:PublicKeyPath"] = keyFile,
                    ["Grpc:RetirementMaintenanceKey"] = credential + "-Maintenance",
                    ["Grpc:RetirementIncidentKey"] = credential + "-Incident",
                    ["Grpc:IncidentRegistrationKey"] = credential + "-Incident-registration",
                    ["Grpc:MaintenanceRegistrationKey"] = credential + "-Maintenance-registration",
                    ["Jwt:Issuer"] = "test-issuer", ["Jwt:Audience"] = "test-audience"
                });
                builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0, l => l.Protocols = HttpProtocols.Http2));
                builder.Services.AddServiceScaffold(builder.Configuration);
                builder.Services.AddInfrastructure(builder.Configuration);
                builder.Services.AddEquipmentServiceAuthentication();
                builder.Services.AddScoped<EquipmentLookupQuery>();
                builder.Services.AddGrpc();
                app = builder.Build();
                app.UseAuthentication(); app.UseAuthorization();
                app.MapGrpcService<EquipmentGrpcService>();
                MedEquipSLA.Equipment.Api.RetirementEndpoints.MapRetirement(app);
                await app.StartAsync();
                var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                return new(database, app, GrpcChannel.ForAddress(address), asset, credential, rsa, keyFile);
            }
            catch
            {
                if (app is not null) await app.DisposeAsync();
                rsa.Dispose(); File.Delete(keyFile); await database.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            channel.Dispose();
            await app.StopAsync(); await app.DisposeAsync();
            signingKey.Dispose(); File.Delete(publicKeyFile);
            await database.DisposeAsync();
        }
    }
}
