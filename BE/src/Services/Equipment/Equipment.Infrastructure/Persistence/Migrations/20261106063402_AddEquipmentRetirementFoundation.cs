using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Equipment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentRetirementFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CancelIntentId",
                table: "EquipmentUseRegistrations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentIdSnapshot",
                table: "EquipmentUseRegistrations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepartmentNameSnapshot",
                table: "EquipmentUseRegistrations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EquipmentCodeSnapshot",
                table: "EquipmentUseRegistrations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EquipmentNameSnapshot",
                table: "EquipmentUseRegistrations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FencingToken",
                table: "EquipmentUseRegistrations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<byte[]>(
                name: "RequestHash",
                table: "EquipmentUseRegistrations",
                type: "binary(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestPayloadJson",
                table: "EquipmentUseRegistrations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EquipmentRetirementIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetirementEpoch = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<byte[]>(type: "binary(32)", maxLength: 32, nullable: false),
                    RequiredSourcesJson = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CancelRequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CancelRequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ContinueCount = table.Column<int>(type: "int", nullable: false),
                    LastContinuedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastContinueReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LastContinuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentRetirementIntents", x => x.Id);
                    table.UniqueConstraint("AK_EquipmentRetirementIntents_Id_EquipmentId_RetirementEpoch", x => new { x.Id, x.EquipmentId, x.RetirementEpoch });
                    table.CheckConstraint("CK_RetirementIntents_Cancellation", "([State] IN (N'Cancelling',N'Cancelled') AND [CancelRequestedByUserId] IS NOT NULL AND [CancelRequestedAtUtc] IS NOT NULL AND [CancelReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CancelReason]))) > 0) OR ([State] NOT IN (N'Cancelling',N'Cancelled') AND [CancelRequestedByUserId] IS NULL AND [CancelRequestedAtUtc] IS NULL AND [CancelReason] IS NULL)");
                    table.CheckConstraint("CK_RetirementIntents_Cancelled", "([State] = N'Cancelled' AND [CancelledAtUtc] IS NOT NULL) OR ([State] <> N'Cancelled' AND [CancelledAtUtc] IS NULL)");
                    table.CheckConstraint("CK_RetirementIntents_Completion", "([State] = N'Completed' AND [CompletedAtUtc] IS NOT NULL) OR ([State] <> N'Completed' AND [CompletedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_RetirementIntents_Coverage", "[RequiredSourcesJson] = N'{\"Maintenance\":1,\"Incident\":1}'");
                    table.CheckConstraint("CK_RetirementIntents_Epoch", "[RetirementEpoch] > 0 AND [ContinueCount] >= 0");
                    table.CheckConstraint("CK_RetirementIntents_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
                    table.CheckConstraint("CK_RetirementIntents_State", "[State] IN (N'Coordinating',N'Ready',N'Cancelling',N'Cancelled',N'Completed')");
                    table.CheckConstraint("CK_RetirementIntents_Utc", "DATEPART(TZOFFSET,[CreatedAtUtc])=0 AND DATEPART(TZOFFSET,[UpdatedAtUtc])=0 AND [UpdatedAtUtc] >= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_EquipmentRetirementIntents_EquipmentAssets_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "EquipmentAssets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RetirementSourceReceipts",
                columns: table => new
                {
                    IntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceService = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetirementEpoch = table.Column<long>(type: "bigint", nullable: false),
                    CoverageVersion = table.Column<int>(type: "int", nullable: false),
                    SourceGateVersion = table.Column<long>(type: "bigint", nullable: false),
                    ReadyProofId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelAcknowledgementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceState = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetirementSourceReceipts", x => new { x.IntentId, x.SourceService });
                    table.CheckConstraint("CK_RetirementReceipts_Proof", "([SourceState] IN (N'Frozen',N'Blocked') AND [ReadyProofId] IS NULL AND [CancelAcknowledgementId] IS NULL) OR ([SourceState]=N'Sealed' AND [ReadyProofId] IS NOT NULL AND [CancelAcknowledgementId] IS NULL) OR ([SourceState]=N'CancelAck' AND [CancelAcknowledgementId] IS NOT NULL)");
                    table.CheckConstraint("CK_RetirementReceipts_Source", "[SourceService] IN (N'Maintenance',N'Incident')");
                    table.CheckConstraint("CK_RetirementReceipts_State", "[SourceState] IN (N'Frozen',N'Blocked',N'Sealed',N'CancelAck')");
                    table.CheckConstraint("CK_RetirementReceipts_Utc", "DATEPART(TZOFFSET,[ReceivedAtUtc])=0");
                    table.CheckConstraint("CK_RetirementReceipts_Version", "[CoverageVersion]=1 AND [RetirementEpoch]>0 AND [SourceGateVersion]>0");
                    table.ForeignKey(
                        name: "FK_RetirementSourceReceipts_EquipmentRetirementIntents_IntentId_EquipmentId_RetirementEpoch",
                        columns: x => new { x.IntentId, x.EquipmentId, x.RetirementEpoch },
                        principalTable: "EquipmentRetirementIntents",
                        principalColumns: new[] { "Id", "EquipmentId", "RetirementEpoch" });
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_EquipmentUseRegistrations_Fence",
                table: "EquipmentUseRegistrations",
                sql: "[FencingToken] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EquipmentUseRegistrations_Payload",
                table: "EquipmentUseRegistrations",
                sql: "[RequestPayloadJson] IS NULL OR (ISJSON([RequestPayloadJson]) = 1 AND [ResourceType] = N'WorkOrder' AND [SourceService] = N'Maintenance' AND [RequestHash] IS NOT NULL AND [FencingToken] > 0 AND [EquipmentCodeSnapshot] IS NOT NULL AND [EquipmentNameSnapshot] IS NOT NULL AND [DepartmentIdSnapshot] IS NOT NULL AND [DepartmentNameSnapshot] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EquipmentUseRegistrations_ProtocolPair",
                table: "EquipmentUseRegistrations",
                sql: "([RequestHash] IS NULL AND [RequestPayloadJson] IS NULL AND [FencingToken] = 0 AND [CancelIntentId] IS NULL AND [EquipmentCodeSnapshot] IS NULL AND [EquipmentNameSnapshot] IS NULL AND [DepartmentIdSnapshot] IS NULL AND [DepartmentNameSnapshot] IS NULL) OR ([RequestHash] IS NOT NULL AND [RequestPayloadJson] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentRetirementIntents_ActorUserId_RequestKey",
                table: "EquipmentRetirementIntents",
                columns: new[] { "ActorUserId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentRetirementIntents_EquipmentId_RetirementEpoch",
                table: "EquipmentRetirementIntents",
                columns: new[] { "EquipmentId", "RetirementEpoch" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentRetirementIntents_State_CreatedAtUtc_Id",
                table: "EquipmentRetirementIntents",
                columns: new[] { "State", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_RetirementIntents_ActiveEquipment",
                table: "EquipmentRetirementIntents",
                column: "EquipmentId",
                unique: true,
                filter: "[State] <> N'Cancelled' AND [State] <> N'Completed'");

            migrationBuilder.CreateIndex(
                name: "IX_RetirementSourceReceipts_EquipmentId_RetirementEpoch_SourceService",
                table: "RetirementSourceReceipts",
                columns: new[] { "EquipmentId", "RetirementEpoch", "SourceService" });

            migrationBuilder.CreateIndex(
                name: "IX_RetirementSourceReceipts_IntentId_EquipmentId_RetirementEpoch",
                table: "RetirementSourceReceipts",
                columns: new[] { "IntentId", "EquipmentId", "RetirementEpoch" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never lose durable protocol history, including terminal intents/proofs/tombstones.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.EquipmentRetirementIntents)
                   OR EXISTS (SELECT 1 FROM dbo.RetirementSourceReceipts)
                   OR EXISTS (SELECT 1 FROM dbo.EquipmentUseRegistrations WHERE
                       RequestHash IS NOT NULL OR RequestPayloadJson IS NOT NULL OR FencingToken <> 0
                       OR CancelIntentId IS NOT NULL OR EquipmentCodeSnapshot IS NOT NULL
                       OR EquipmentNameSnapshot IS NOT NULL OR DepartmentIdSnapshot IS NOT NULL
                       OR DepartmentNameSnapshot IS NOT NULL OR (ResourceType=N'WorkOrder' AND State=N'Aborted'))
                    THROW 51021, 'Stage 2.1 rollback refused: durable Equipment protocol data exists.', 1;
                """);

            migrationBuilder.DropTable(
                name: "RetirementSourceReceipts");

            migrationBuilder.DropTable(
                name: "EquipmentRetirementIntents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EquipmentUseRegistrations_Fence",
                table: "EquipmentUseRegistrations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EquipmentUseRegistrations_Payload",
                table: "EquipmentUseRegistrations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EquipmentUseRegistrations_ProtocolPair",
                table: "EquipmentUseRegistrations");

            migrationBuilder.DropColumn(
                name: "CancelIntentId",
                table: "EquipmentUseRegistrations");

            migrationBuilder.DropColumn(
                name: "DepartmentIdSnapshot",
                table: "EquipmentUseRegistrations");

            migrationBuilder.DropColumn(
                name: "DepartmentNameSnapshot",
                table: "EquipmentUseRegistrations");

            migrationBuilder.DropColumn(
                name: "EquipmentCodeSnapshot",
                table: "EquipmentUseRegistrations");

            migrationBuilder.DropColumn(
                name: "EquipmentNameSnapshot",
                table: "EquipmentUseRegistrations");

            migrationBuilder.DropColumn(
                name: "FencingToken",
                table: "EquipmentUseRegistrations");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "EquipmentUseRegistrations");

            migrationBuilder.DropColumn(
                name: "RequestPayloadJson",
                table: "EquipmentUseRegistrations");
        }
    }
}
