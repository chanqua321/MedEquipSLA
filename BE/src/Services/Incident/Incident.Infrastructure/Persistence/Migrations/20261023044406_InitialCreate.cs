using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Incident.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorService = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                    table.CheckConstraint("CK_AuditEntries_Json", "[ChangesJson] IS NULL OR ISJSON([ChangesJson]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRequests",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorScope = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OperationName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestHash = table.Column<byte[]>(type: "binary(32)", maxLength: 32, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRequests", x => x.OperationId);
                    table.CheckConstraint("CK_IdempotencyRequests_Expiration", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.CheckConstraint("CK_IdempotencyRequests_Json", "[ResponseJson] IS NULL OR ISJSON([ResponseJson]) = 1");
                    table.CheckConstraint("CK_IdempotencyRequests_State", "[State] IN (N'Pending',N'Committed',N'Aborted')");
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeaseOwner = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.EventId);
                    table.CheckConstraint("CK_OutboxMessages_Json", "ISJSON([PayloadJson]) = 1");
                    table.CheckConstraint("CK_OutboxMessages_Status", "[Status] IN (N'Pending',N'Processing',N'Published',N'Failed')");
                    table.CheckConstraint("CK_OutboxMessages_Versions", "[SchemaVersion] >= 1 AND [AggregateVersion] >= 1 AND [AttemptCount] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "SlaPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaPolicies", x => x.Id);
                    table.CheckConstraint("CK_SlaPolicies_Priority", "[Priority] IN (N'Critical',N'High',N'Medium',N'Low')");
                });

            migrationBuilder.CreateTable(
                name: "SlaPolicyVersions",
                columns: table => new
                {
                    SlaPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ResponseDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    ResolutionDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaPolicyVersions", x => new { x.SlaPolicyId, x.Version });
                    table.CheckConstraint("CK_SlaPolicyVersions_Durations", "[Version] > 0 AND [ResponseDurationMinutes] > 0 AND [ResolutionDurationMinutes] >= [ResponseDurationMinutes]");
                    table.CheckConstraint("CK_SlaPolicyVersions_EffectiveInterval", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                    table.ForeignKey(
                        name: "FK_SlaPolicyVersions_SlaPolicies_SlaPolicyId",
                        column: x => x.SlaPolicyId,
                        principalTable: "SlaPolicies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentCodeSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EquipmentNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DepartmentIdSnapshot = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedIncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SlaPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SlaPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    SlaPrioritySnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResponseDurationMinutesSnapshot = table.Column<int>(type: "int", nullable: false),
                    ResolutionDurationMinutesSnapshot = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResponseDeadlineAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolutionDeadlineAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FirstRespondedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResponseOutcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResolutionOutcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SlaFinalizedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EquipmentUseRegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                    table.CheckConstraint("CK_Incidents_AggregateVersion", "[AggregateVersion] >= 1");
                    table.CheckConstraint("CK_Incidents_Finalization", "([Status] = N'Closed' AND [ClosedAtUtc] IS NOT NULL AND [SlaFinalizedAtUtc] IS NOT NULL AND [ResponseOutcome] <> N'Pending' AND [ResolutionOutcome] <> N'Pending') OR ([Status] <> N'Closed' AND [ClosedAtUtc] IS NULL AND [SlaFinalizedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_Incidents_Priority", "[Priority] IN (N'Critical',N'High',N'Medium',N'Low')");
                    table.CheckConstraint("CK_Incidents_RelatedIncident", "[RelatedIncidentId] IS NULL OR [RelatedIncidentId] <> [Id]");
                    table.CheckConstraint("CK_Incidents_ResolutionOutcome", "[ResolutionOutcome] IN (N'Pending',N'Met',N'Breached')");
                    table.CheckConstraint("CK_Incidents_ResponseOutcome", "[ResponseOutcome] IN (N'Pending',N'Met',N'Breached')");
                    table.CheckConstraint("CK_Incidents_SlaDeadlines", "[ResponseDeadlineAtUtc] = DATEADD(minute,[ResponseDurationMinutesSnapshot],[CreatedAtUtc]) AND [ResolutionDeadlineAtUtc] = DATEADD(minute,[ResolutionDurationMinutesSnapshot],[CreatedAtUtc])");
                    table.CheckConstraint("CK_Incidents_SlaDurations", "[ResponseDurationMinutesSnapshot] > 0 AND [ResolutionDurationMinutesSnapshot] >= [ResponseDurationMinutesSnapshot]");
                    table.CheckConstraint("CK_Incidents_SlaPrioritySnapshot", "[SlaPrioritySnapshot] IN (N'Critical',N'High',N'Medium',N'Low')");
                    table.CheckConstraint("CK_Incidents_Status", "[Status] IN (N'Open',N'InProgress',N'Resolved',N'Closed')");
                    table.ForeignKey(
                        name: "FK_Incidents_Incidents_RelatedIncidentId",
                        column: x => x.RelatedIncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Incidents_SlaPolicyVersions_SlaPolicyId_SlaPolicyVersion",
                        columns: x => new { x.SlaPolicyId, x.SlaPolicyVersion },
                        principalTable: "SlaPolicyVersions",
                        principalColumns: new[] { "SlaPolicyId", "Version" });
                });

            migrationBuilder.CreateTable(
                name: "IncidentActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FromTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentActivities_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ResolutionAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ResultSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewDecision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResolutionAttempts", x => x.Id);
                    table.CheckConstraint("CK_ResolutionAttempts_AttemptNumber", "[AttemptNumber] > 0");
                    table.CheckConstraint("CK_ResolutionAttempts_RejectedReason", "[ReviewDecision] <> N'Rejected' OR NULLIF(LTRIM(RTRIM([ReviewReason])),N'') IS NOT NULL");
                    table.CheckConstraint("CK_ResolutionAttempts_Review", "([ReviewDecision] = N'Pending' AND [ReviewedByUserId] IS NULL AND [ReviewedAtUtc] IS NULL) OR ([ReviewDecision] IN (N'Accepted',N'Rejected') AND [ReviewedByUserId] IS NOT NULL AND [ReviewedAtUtc] IS NOT NULL AND [ReviewedAtUtc] >= [CompletedAtUtc])");
                    table.CheckConstraint("CK_ResolutionAttempts_ReviewDecision", "[ReviewDecision] IN (N'Pending',N'Accepted',N'Rejected')");
                    table.ForeignKey(
                        name: "FK_ResolutionAttempts_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SlaDeadlineAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlertKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DeadlineAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DetectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaDeadlineAlerts", x => x.Id);
                    table.CheckConstraint("CK_SlaDeadlineAlerts_AfterDeadline", "[DetectedAtUtc] > [DeadlineAtUtc]");
                    table.CheckConstraint("CK_SlaDeadlineAlerts_AlertKind", "[AlertKind] IN (N'Response',N'Resolution')");
                    table.ForeignKey(
                        name: "FK_SlaDeadlineAlerts_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityType_EntityId_OccurredAtUtc",
                table: "AuditEntries",
                columns: new[] { "EntityType", "EntityId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_IdempotencyRequests_ActorScope_OperationName_IdempotencyKey",
                table: "IdempotencyRequests",
                columns: new[] { "ActorScope", "OperationName", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncidentActivities_IncidentId_OccurredAtUtc_Id",
                table: "IncidentActivities",
                columns: new[] { "IncidentId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_AssignedTechnicianId_Status",
                table: "Incidents",
                columns: new[] { "AssignedTechnicianId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_DepartmentIdSnapshot_CreatedAtUtc_Id",
                table: "Incidents",
                columns: new[] { "DepartmentIdSnapshot", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_EquipmentId_Status",
                table: "Incidents",
                columns: new[] { "EquipmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_RelatedIncidentId",
                table: "Incidents",
                column: "RelatedIncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_SlaPolicyId_SlaPolicyVersion",
                table: "Incidents",
                columns: new[] { "SlaPolicyId", "SlaPolicyVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Status_ResolutionDeadlineAtUtc",
                table: "Incidents",
                columns: new[] { "Status", "ResolutionDeadlineAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Status_ResponseDeadlineAtUtc",
                table: "Incidents",
                columns: new[] { "Status", "ResponseDeadlineAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Incidents_Code",
                table: "Incidents",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAtUtc_OccurredAtUtc",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptAtUtc", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_ResolutionAttempts_IncidentId",
                table: "ResolutionAttempts",
                column: "IncidentId",
                unique: true,
                filter: "[ReviewDecision] = N'Accepted'");

            migrationBuilder.CreateIndex(
                name: "UX_ResolutionAttempts_IncidentId_AttemptNumber",
                table: "ResolutionAttempts",
                columns: new[] { "IncidentId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SlaDeadlineAlerts_EventId",
                table: "SlaDeadlineAlerts",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SlaDeadlineAlerts_IncidentId_AlertKind",
                table: "SlaDeadlineAlerts",
                columns: new[] { "IncidentId", "AlertKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SlaPolicies_Priority",
                table: "SlaPolicies",
                column: "Priority",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SlaPolicyVersions_SlaPolicyId_EffectiveFromUtc",
                table: "SlaPolicyVersions",
                columns: new[] { "SlaPolicyId", "EffectiveFromUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "IdempotencyRequests");

            migrationBuilder.DropTable(
                name: "IncidentActivities");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "ResolutionAttempts");

            migrationBuilder.DropTable(
                name: "SlaDeadlineAlerts");

            migrationBuilder.DropTable(
                name: "Incidents");

            migrationBuilder.DropTable(
                name: "SlaPolicyVersions");

            migrationBuilder.DropTable(
                name: "SlaPolicies");
        }
    }
}
