using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Equipment.Infrastructure.Persistence.Migrations
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
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentCategories", x => x.Id);
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
                name: "InboxMessages",
                columns: table => new
                {
                    ConsumerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => new { x.ConsumerName, x.EventId });
                    table.CheckConstraint("CK_InboxMessages_SchemaVersion", "[SchemaVersion] >= 1");
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
                name: "ReconciliationCheckpoints",
                columns: table => new
                {
                    SourceService = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SnapshotWatermark = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Cursor = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastCompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationCheckpoints", x => x.SourceService);
                    table.CheckConstraint("CK_ReconciliationCheckpoints_SourceService", "[SourceService] IN (N'Incident',N'Maintenance')");
                    table.CheckConstraint("CK_ReconciliationCheckpoints_State", "[State] IN (N'Idle',N'Running',N'Completed',N'Failed')");
                });

            migrationBuilder.CreateTable(
                name: "EquipmentAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PurchasedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    WarrantyUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    IsRetired = table.Column<bool>(type: "bit", nullable: false),
                    OperationalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RetiredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RetiredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetirementReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentAssets", x => x.Id);
                    table.CheckConstraint("CK_EquipmentAssets_AggregateVersion", "[AggregateVersion] >= 1");
                    table.CheckConstraint("CK_EquipmentAssets_OperationalStatus", "[OperationalStatus] IN (N'Operational',N'UnderMaintenance',N'Broken',N'Retired')");
                    table.CheckConstraint("CK_EquipmentAssets_Retirement", "([IsRetired] = 1 AND [OperationalStatus] = N'Retired' AND [RetiredAtUtc] IS NOT NULL AND [RetiredByUserId] IS NOT NULL) OR ([IsRetired] = 0 AND [OperationalStatus] <> N'Retired' AND [RetiredAtUtc] IS NULL)");
                    table.ForeignKey(
                        name: "FK_EquipmentAssets_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EquipmentAssets_EquipmentCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "EquipmentCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EquipmentStateProjections",
                columns: table => new
                {
                    SourceService = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceAggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsBlocking = table.Column<bool>(type: "bit", nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    LastEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastOccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentStateProjections", x => new { x.SourceService, x.SourceAggregateId });
                    table.CheckConstraint("CK_EquipmentStateProjections_SourceService", "[SourceService] IN (N'Incident',N'Maintenance')");
                    table.CheckConstraint("CK_EquipmentStateProjections_SourceVersion", "[SourceVersion] >= 1");
                    table.ForeignKey(
                        name: "FK_EquipmentStateProjections_EquipmentAssets_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "EquipmentAssets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EquipmentStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeKind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FromDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FromDepartmentNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ToDepartmentNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentStatusHistories_Departments_FromDepartmentId",
                        column: x => x.FromDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EquipmentStatusHistories_Departments_ToDepartmentId",
                        column: x => x.ToDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EquipmentStatusHistories_EquipmentAssets_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "EquipmentAssets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EquipmentUseRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceService = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    RegisteredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastVerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentUseRegistrations", x => x.Id);
                    table.CheckConstraint("CK_EquipmentUseRegistrations_ResourceType", "[ResourceType] IN (N'Incident',N'MaintenanceSchedule',N'WorkOrder')");
                    table.CheckConstraint("CK_EquipmentUseRegistrations_SourceService", "[SourceService] IN (N'Incident',N'Maintenance')");
                    table.CheckConstraint("CK_EquipmentUseRegistrations_SourceVersion", "[SourceVersion] >= 0");
                    table.CheckConstraint("CK_EquipmentUseRegistrations_State", "[State] IN (N'Pending',N'Active',N'Released',N'Aborted')");
                    table.ForeignKey(
                        name: "FK_EquipmentUseRegistrations_EquipmentAssets_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "EquipmentAssets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityType_EntityId_OccurredAtUtc",
                table: "AuditEntries",
                columns: new[] { "EntityType", "EntityId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentAssets_CategoryId",
                table: "EquipmentAssets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentAssets_DepartmentId_OperationalStatus_Id",
                table: "EquipmentAssets",
                columns: new[] { "DepartmentId", "OperationalStatus", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_EquipmentAssets_AssetCode",
                table: "EquipmentAssets",
                column: "AssetCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EquipmentAssets_SerialNumber",
                table: "EquipmentAssets",
                column: "SerialNumber",
                unique: true,
                filter: "[SerialNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_EquipmentCategories_Code",
                table: "EquipmentCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentStateProjections_EquipmentId_IsBlocking",
                table: "EquipmentStateProjections",
                columns: new[] { "EquipmentId", "IsBlocking" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentStatusHistories_EquipmentId_ChangedAtUtc_Id",
                table: "EquipmentStatusHistories",
                columns: new[] { "EquipmentId", "ChangedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentStatusHistories_FromDepartmentId",
                table: "EquipmentStatusHistories",
                column: "FromDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentStatusHistories_ToDepartmentId",
                table: "EquipmentStatusHistories",
                column: "ToDepartmentId");

            migrationBuilder.CreateIndex(
                name: "UX_EquipmentStatusHistories_SourceEventId",
                table: "EquipmentStatusHistories",
                column: "SourceEventId",
                unique: true,
                filter: "[SourceEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentUseRegistrations_EquipmentId_State",
                table: "EquipmentUseRegistrations",
                columns: new[] { "EquipmentId", "State" });

            migrationBuilder.CreateIndex(
                name: "UX_EquipmentUseRegistrations_SourceService_ResourceType_ResourceId",
                table: "EquipmentUseRegistrations",
                columns: new[] { "SourceService", "ResourceType", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EquipmentUseRegistrations_SourceService_SourceOperationId",
                table: "EquipmentUseRegistrations",
                columns: new[] { "SourceService", "SourceOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_IdempotencyRequests_ActorScope_OperationName_IdempotencyKey",
                table: "IdempotencyRequests",
                columns: new[] { "ActorScope", "OperationName", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAtUtc_OccurredAtUtc",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptAtUtc", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "EquipmentStateProjections");

            migrationBuilder.DropTable(
                name: "EquipmentStatusHistories");

            migrationBuilder.DropTable(
                name: "EquipmentUseRegistrations");

            migrationBuilder.DropTable(
                name: "IdempotencyRequests");

            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "ReconciliationCheckpoints");

            migrationBuilder.DropTable(
                name: "EquipmentAssets");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "EquipmentCategories");
        }
    }
}
