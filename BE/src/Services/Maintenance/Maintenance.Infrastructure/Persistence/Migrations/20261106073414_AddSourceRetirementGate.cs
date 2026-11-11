using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maintenance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceRetirementGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EquipmentId",
                table: "IdempotencyRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestPayloadJson",
                table: "IdempotencyRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EquipmentRetirementGates",
                columns: table => new
                {
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentIntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HighestRetirementEpoch = table.Column<long>(type: "bigint", nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceGateVersion = table.Column<long>(type: "bigint", nullable: false),
                    ReadyProofId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelAcknowledgementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentRetirementGates", x => x.EquipmentId);
                    table.CheckConstraint("CK_SourceRetirementGate_Binding", "([HighestRetirementEpoch]=0 AND [CurrentIntentId] IS NULL AND [Phase]=N'Open' AND [ReadyProofId] IS NULL AND [CancelAcknowledgementId] IS NULL) OR ([HighestRetirementEpoch]>0 AND [CurrentIntentId] IS NOT NULL)");
                    table.CheckConstraint("CK_SourceRetirementGate_Phase", "[Phase] IN (N'Open',N'Frozen',N'Sealed',N'Retired')");
                    table.CheckConstraint("CK_SourceRetirementGate_Proof", "([Phase]=N'Frozen' AND [ReadyProofId] IS NULL AND [CancelAcknowledgementId] IS NULL) OR ([Phase] IN (N'Sealed',N'Retired') AND [ReadyProofId] IS NOT NULL AND [CancelAcknowledgementId] IS NULL) OR ([Phase]=N'Open' AND ([HighestRetirementEpoch]=0 OR [CancelAcknowledgementId] IS NOT NULL))");
                    table.CheckConstraint("CK_SourceRetirementGate_Utc", "DATEPART(TZOFFSET,[CreatedAtUtc])=0 AND DATEPART(TZOFFSET,[UpdatedAtUtc])=0 AND [UpdatedAtUtc]>=[CreatedAtUtc]");
                    table.CheckConstraint("CK_SourceRetirementGate_Version", "[HighestRetirementEpoch]>=0 AND [SourceGateVersion]>=0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRequests_EquipmentId_State_OperationName",
                table: "IdempotencyRequests",
                columns: new[] { "EquipmentId", "State", "OperationName" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_IdempotencyRequests_RequestPayload",
                table: "IdempotencyRequests",
                sql: "[RequestPayloadJson] IS NULL OR ISJSON([RequestPayloadJson])=1");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentRetirementGates_Phase_UpdatedAtUtc_EquipmentId",
                table: "EquipmentRetirementGates",
                columns: new[] { "Phase", "UpdatedAtUtc", "EquipmentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS(SELECT 1 FROM dbo.EquipmentRetirementGates)
                    OR EXISTS(SELECT 1 FROM dbo.IdempotencyRequests WHERE EquipmentId IS NOT NULL OR RequestPayloadJson IS NOT NULL)
                    THROW 51031, 'Rollback blocked: source retirement protocol metadata exists.', 1;
                """);
            migrationBuilder.DropTable(
                name: "EquipmentRetirementGates");

            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRequests_EquipmentId_State_OperationName",
                table: "IdempotencyRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IdempotencyRequests_RequestPayload",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "EquipmentId",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "RequestPayloadJson",
                table: "IdempotencyRequests");
        }
    }
}
