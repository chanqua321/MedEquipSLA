using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MedEquipSLA.Hosting;
using MedEquipSLA.Maintenance.Api;
using MedEquipSLA.Maintenance.Application.Features.Schedules;
using MedEquipSLA.Maintenance.Infrastructure;
using MedEquipSLA.Maintenance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using static MedEquipSLA.Database.IntegrationTests.EquipmentWorkOrderGrpcTests;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class MaintenanceApiTests
{
    [SqlServerFact]
    public async Task Swagger_builds_and_schedule_creation_is_admin_only_with_idempotent_real_registration()
    {
        await using var host = await Host.CreateAsync();
        using var http = host.Http();
        using var swagger = await http.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        using var document = JsonDocument.Parse(await swagger.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/work-orders/{id}/attempts", out _));
        foreach (var role in new[] { "DepartmentStaff", "Technician" })
        {
            http.DefaultRequestHeaders.Authorization = new("Bearer", host.Issuer.AdminToken(role));
            using var denied = await http.PostAsJsonAsync("/api/maintenance-schedules", host.ScheduleInput());
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        http.DefaultRequestHeaders.Authorization = new("Bearer", host.Issuer.AdminToken());
        var key = Guid.NewGuid(); http.DefaultRequestHeaders.Add("Idempotency-Key", key.ToString());
        using var created = await http.PostAsJsonAsync("/api/maintenance-schedules", host.ScheduleInput());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = (await created.Content.ReadFromJsonAsync<ScheduleDetail>())!;
        using var replay = await http.PostAsJsonAsync("/api/maintenance-schedules", host.ScheduleInput());
        Assert.Equal(original.Id, (await replay.Content.ReadFromJsonAsync<ScheduleDetail>())!.Id);
        await using var db = host.Context();
        Assert.Single(await db.MaintenanceSchedules.ToArrayAsync());
        Assert.Single(await db.OutboxMessages.ToArrayAsync());
    }

    [SqlServerFact]
    public async Task Technician_workflow_requires_assignment_photo_and_calibration_evidence_before_admin_acceptance()
    {
        await using var host = await Host.CreateAsync();
        using var http = host.Http(); http.DefaultRequestHeaders.Authorization = new("Bearer", host.Issuer.AdminToken());
        http.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var created = await http.PostAsJsonAsync("/api/maintenance-schedules", host.ScheduleInput());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Guid orderId; var technician = Guid.NewGuid();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaintenanceDbContext>();
            var schedule = await db.MaintenanceSchedules.SingleAsync();
            db.Add(schedule.PlanNext(DateTimeOffset.UtcNow)); await db.SaveChangesAsync();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<MedEquipSLA.Maintenance.Application.Features.WorkOrders.IWorkOrderGeneration>().RunBatchAsync(default));
            var order = await db.WorkOrders.SingleAsync(); orderId = order.Id;
            await new WorkOrderAssignment(db).AssignAsync(order.Id, technician, order.RowVersion, Guid.NewGuid(), default);
        }
        async Task<byte[]> Version()
        {
            await using var db = host.Context();
            return (await db.WorkOrders.AsNoTracking().SingleAsync()).RowVersion;
        }
        var start = new { rowVersion = await Version() };
        http.DefaultRequestHeaders.Authorization = new("Bearer", host.Issuer.AdminToken("Technician", Guid.NewGuid()));
        using var wrong = await http.PostAsJsonAsync($"/api/work-orders/{orderId}/start", start);
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        http.DefaultRequestHeaders.Authorization = new("Bearer", host.Issuer.AdminToken("Technician", technician));
        using var begun = await http.PostAsJsonAsync($"/api/work-orders/{orderId}/start", start);
        Assert.Equal(HttpStatusCode.NoContent, begun.StatusCode);
        using var progress = await http.PostAsJsonAsync($"/api/work-orders/{orderId}/progress", new { rowVersion = await Version(), notes = "Testing the running device" });
        Assert.Equal(HttpStatusCode.NoContent, progress.StatusCode);
        var version = await Version();
        var photo = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jZ1sAAAAASUVORK5CYII=");
        using var body = new MultipartFormDataContent();
        body.Add(new StringContent("Checked with operating machine photo"), "notes");
        body.Add(new StringContent("Passed"), "result");
        body.Add(new StringContent(Convert.ToBase64String(version)), "rowVersion");
        body.Add(new StringContent("[{\"code\":\"TEMP\",\"name\":\"Temperature\",\"unit\":\"C\",\"value\":5,\"minimum\":0,\"maximum\":10}]"), "measurementsJson");
        body.Add(new ByteArrayContent(photo), "photo", "equipment.png");
        using var submitted = await http.PostAsync($"/api/work-orders/{orderId}/attempts", body);
        Assert.Equal(HttpStatusCode.NoContent, submitted.StatusCode);
        Guid attempt;
        await using (var db = host.Context()) attempt = (await db.WorkOrderAttempts.SingleAsync()).Id;
        var returned = await http.GetByteArrayAsync($"/api/work-orders/{orderId}/attempts/{attempt}/photo");
        Assert.Equal(photo, returned);
        using var forbiddenReview = await http.PostAsJsonAsync($"/api/work-orders/{orderId}/review", new { accept = true, rowVersion = await Version() });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenReview.StatusCode);
        http.DefaultRequestHeaders.Authorization = new("Bearer", host.Issuer.AdminToken());
        using var accepted = await http.PostAsJsonAsync($"/api/work-orders/{orderId}/review", new { accept = true, rowVersion = await Version() });
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        await using var check = host.Context();
        Assert.Equal("Closed", (await check.WorkOrders.SingleAsync()).Status);
    }

    internal sealed class Host : IAsyncDisposable
    {
        internal ProtocolHost Issuer { get; }
        private readonly TestDatabase catalog;
        private readonly WebApplication app;
        private readonly DateTimeOffset anchor = DateTimeOffset.UtcNow.AddMinutes(-10);
        internal IServiceProvider Services => app.Services;
        private Host(ProtocolHost issuer, TestDatabase catalog, WebApplication app) { Issuer = issuer; this.catalog = catalog; this.app = app; }
        internal MaintenanceDbContext Context() => (MaintenanceDbContext)Contexts.Create("Maintenance", catalog.ConnectionString);
        internal object ScheduleInput() => new { equipmentId = Issuer.AssetId, workType = "Calibration",
            timing = new { anchorAtUtc = anchor, intervalDays = 7, reminderLeadMinutes = 60, completionWindowMinutes = 30, isActive = true } };
        internal HttpClient Http() => new() { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        internal static async Task<Host> CreateAsync()
        {
            var issuer = await ProtocolHost.CreateAsync();
            var catalog = await TestDatabase.CreateAsync("Maintenance");
            WebApplication? app = null;
            try
            {
                await using (var db = Contexts.Create("Maintenance", catalog.ConnectionString)) await db.Database.MigrateAsync();
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
                builder.Configuration.Sources.Clear();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
                {
                    ["ConnectionStrings:Database"] = catalog.ConnectionString, ["Workers:Enabled"] = "false",
                    ["Grpc:MaintenanceRegistrationKey"] = issuer.RegistrationCredentials("Maintenance").GetValue("x-maintenance-registration-key"),
                    ["Grpc:MaintenanceWorkOrderKey"] = issuer.Credentials.GetValue("x-maintenance-workorder-key"),
                    ["Grpc:RetirementSourceKey"] = issuer.RetirementCredentials("Maintenance").GetValue("x-retirement-source-key"),
                    ["Jwt:PublicKeyPath"] = issuer.VerificationKeyFile, ["Jwt:Issuer"] = "test-issuer", ["Jwt:Audience"] = "test-audience"
                });
                builder.Logging.ClearProviders();
                builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0));
                builder.Services.AddServiceScaffold(builder.Configuration);
                builder.Services.AddInfrastructure(builder.Configuration);
                builder.Services.AddSingleton(issuer.Client);
                app = builder.Build();
                app.UseServiceScaffold("Maintenance");
                app.MapWorkOrders(); app.MapWorkOrderOperations(); app.MapWorkOrderWorkflow(); app.MapScheduleQueries(); app.MapSourceOperations();
                await app.StartAsync(); return new(issuer, catalog, app);
            }
            catch { if (app is not null) await app.DisposeAsync(); await catalog.DisposeAsync(); await issuer.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        { await app.StopAsync(); await app.DisposeAsync(); await catalog.DisposeAsync(); await Issuer.DisposeAsync(); }
    }
}
