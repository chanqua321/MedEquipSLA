using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maintenance.Infrastructure.Persistence.Migrations
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
                name: "MaintenanceSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentCodeSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EquipmentNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DepartmentIdSnapshot = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WorkType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AnchorAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IntervalDays = table.Column<int>(type: "int", nullable: false),
                    NextOccurrenceAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReminderLeadMinutes = table.Column<int>(type: "int", nullable: false),
                    CompletionWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EquipmentUseRegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceSchedules", x => x.Id);
                    table.CheckConstraint("CK_MaintenanceSchedules_AggregateVersion", "[AggregateVersion] >= 1");
                    table.CheckConstraint("CK_MaintenanceSchedules_Intervals", "[IntervalDays] > 0 AND [ReminderLeadMinutes] >= 0 AND [CompletionWindowMinutes] >= 0");
                    table.CheckConstraint("CK_MaintenanceSchedules_WorkType", "[WorkType] IN (N'Maintenance',N'Calibration')");
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
                name: "ScheduleOccurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledOccurrenceAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleOccurrences", x => x.Id);
                    table.CheckConstraint("CK_ScheduleOccurrences_Deadline", "[DueAtUtc] >= [ScheduledOccurrenceAtUtc]");
                    table.CheckConstraint("CK_ScheduleOccurrences_State", "[State] IN (N'Planned',N'WorkOrderCreated',N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_ScheduleOccurrences_MaintenanceSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "MaintenanceSchedules",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OccurrenceReminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurrenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReminderKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TriggeredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OccurrenceReminders", x => x.Id);
                    table.CheckConstraint("CK_OccurrenceReminders_ReminderKind", "[ReminderKind] IN (N'DueSoon',N'Overdue')");
                    table.ForeignKey(
                        name: "FK_OccurrenceReminders_ScheduleOccurrences_OccurrenceId",
                        column: x => x.OccurrenceId,
                        principalTable: "ScheduleOccurrences",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OccurrenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentCodeSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EquipmentNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DepartmentIdSnapshot = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WorkType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssignedTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScheduledForUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsBlocking = table.Column<bool>(type: "bit", nullable: false),
                    EquipmentUseRegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                    table.CheckConstraint("CK_WorkOrders_AggregateVersion", "[AggregateVersion] >= 1");
                    table.CheckConstraint("CK_WorkOrders_Deadline", "[DueAtUtc] >= [ScheduledForUtc]");
                    table.CheckConstraint("CK_WorkOrders_Status", "[Status] IN (N'Scheduled',N'InProgress',N'Completed',N'Closed',N'Cancelled')");
                    table.CheckConstraint("CK_WorkOrders_WorkType", "[WorkType] IN (N'Maintenance',N'Calibration')");
                    table.ForeignKey(
                        name: "FK_WorkOrders_ScheduleOccurrences_OccurrenceId",
                        column: x => x.OccurrenceId,
                        principalTable: "ScheduleOccurrences",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_WorkOrderActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderActivities_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResultStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrencyCode = table.Column<string>(type: "char(3)", maxLength: 3, nullable: true),
                    ReviewDecision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderAttempts", x => x.Id);
                    table.CheckConstraint("CK_WorkOrderAttempts_AttemptNumber", "[AttemptNumber] > 0");
                    table.CheckConstraint("CK_WorkOrderAttempts_Cost", "[Cost] IS NULL OR [Cost] >= 0");
                    table.CheckConstraint("CK_WorkOrderAttempts_RejectedReason", "[ReviewDecision] <> N'Rejected' OR NULLIF(LTRIM(RTRIM([ReviewReason])),N'') IS NOT NULL");
                    table.CheckConstraint("CK_WorkOrderAttempts_ResultStatus", "[ResultStatus] IN (N'Passed',N'Failed')");
                    table.CheckConstraint("CK_WorkOrderAttempts_Review", "([ReviewDecision] = N'Pending' AND [ReviewedByUserId] IS NULL AND [ReviewedAtUtc] IS NULL) OR ([ReviewDecision] IN (N'Accepted',N'Rejected') AND [ReviewedByUserId] IS NOT NULL AND [ReviewedAtUtc] IS NOT NULL AND [ReviewedAtUtc] >= [CompletedAtUtc])");
                    table.CheckConstraint("CK_WorkOrderAttempts_ReviewDecision", "[ReviewDecision] IN (N'Pending',N'Accepted',N'Rejected')");
                    table.ForeignKey(
                        name: "FK_WorkOrderAttempts_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CalibrationMeasurements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParameterCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ParameterName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MeasuredValue = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    MinimumAllowed = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    MaximumAllowed = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    Passed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalibrationMeasurements", x => x.Id);
                    table.CheckConstraint("CK_CalibrationMeasurements_Result", "([Passed] = 1 AND ([MinimumAllowed] IS NULL OR [MeasuredValue] >= [MinimumAllowed]) AND ([MaximumAllowed] IS NULL OR [MeasuredValue] <= [MaximumAllowed])) OR ([Passed] = 0 AND (([MinimumAllowed] IS NOT NULL AND [MeasuredValue] < [MinimumAllowed]) OR ([MaximumAllowed] IS NOT NULL AND [MeasuredValue] > [MaximumAllowed])))");
                    table.CheckConstraint("CK_CalibrationMeasurements_Thresholds", "([MinimumAllowed] IS NOT NULL OR [MaximumAllowed] IS NOT NULL) AND ([MinimumAllowed] IS NULL OR [MaximumAllowed] IS NULL OR [MinimumAllowed] <= [MaximumAllowed])");
                    table.ForeignKey(
                        name: "FK_CalibrationMeasurements_WorkOrderAttempts_WorkOrderAttemptId",
                        column: x => x.WorkOrderAttemptId,
                        principalTable: "WorkOrderAttempts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityType_EntityId_OccurredAtUtc",
                table: "AuditEntries",
                columns: new[] { "EntityType", "EntityId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_CalibrationMeasurements_WorkOrderAttemptId_ParameterCode",
                table: "CalibrationMeasurements",
                columns: new[] { "WorkOrderAttemptId", "ParameterCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_IdempotencyRequests_ActorScope_OperationName_IdempotencyKey",
                table: "IdempotencyRequests",
                columns: new[] { "ActorScope", "OperationName", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_IsActive_NextOccurrenceAtUtc",
                table: "MaintenanceSchedules",
                columns: new[] { "IsActive", "NextOccurrenceAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_MaintenanceSchedules_Code",
                table: "MaintenanceSchedules",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_OccurrenceReminders_EventId",
                table: "OccurrenceReminders",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_OccurrenceReminders_OccurrenceId_ReminderKind",
                table: "OccurrenceReminders",
                columns: new[] { "OccurrenceId", "ReminderKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAtUtc_OccurredAtUtc",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptAtUtc", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleOccurrences_State_ScheduledOccurrenceAtUtc",
                table: "ScheduleOccurrences",
                columns: new[] { "State", "ScheduledOccurrenceAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_ScheduleOccurrences_ScheduleId_ScheduledOccurrenceAtUtc",
                table: "ScheduleOccurrences",
                columns: new[] { "ScheduleId", "ScheduledOccurrenceAtUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderActivities_WorkOrderId_OccurredAtUtc_Id",
                table: "WorkOrderActivities",
                columns: new[] { "WorkOrderId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_WorkOrderAttempts_WorkOrderId",
                table: "WorkOrderAttempts",
                column: "WorkOrderId",
                unique: true,
                filter: "[ReviewDecision] = N'Accepted'");

            migrationBuilder.CreateIndex(
                name: "UX_WorkOrderAttempts_WorkOrderId_AttemptNumber",
                table: "WorkOrderAttempts",
                columns: new[] { "WorkOrderId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_AssignedTechnicianId_Status",
                table: "WorkOrders",
                columns: new[] { "AssignedTechnicianId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_EquipmentId_Status",
                table: "WorkOrders",
                columns: new[] { "EquipmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_Status_DueAtUtc_Id",
                table: "WorkOrders",
                columns: new[] { "Status", "DueAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_WorkOrders_Code",
                table: "WorkOrders",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_WorkOrders_OccurrenceId",
                table: "WorkOrders",
                column: "OccurrenceId",
                unique: true,
                filter: "[OccurrenceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "CalibrationMeasurements");

            migrationBuilder.DropTable(
                name: "IdempotencyRequests");

            migrationBuilder.DropTable(
                name: "OccurrenceReminders");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "WorkOrderActivities");

            migrationBuilder.DropTable(
                name: "WorkOrderAttempts");

            migrationBuilder.DropTable(
                name: "WorkOrders");

            migrationBuilder.DropTable(
                name: "ScheduleOccurrences");

            migrationBuilder.DropTable(
                name: "MaintenanceSchedules");
        }
    }
}
