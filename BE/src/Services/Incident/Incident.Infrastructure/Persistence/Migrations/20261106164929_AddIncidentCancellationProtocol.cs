using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Incident.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentCancellationProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "IdempotencyRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelIntentId",
                table: "IdempotencyRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "IdempotencyRequests",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelRequestedByUserId",
                table: "IdempotencyRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentAttemptId",
                table: "IdempotencyRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FencingToken",
                table: "IdempotencyRequests",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "InterventionRequired",
                table: "IdempotencyRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAtUtc",
                table: "IdempotencyRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "IdempotencyRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseOwner",
                table: "IdempotencyRequests",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseUntilUtc",
                table: "IdempotencyRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRetryAtUtc",
                table: "IdempotencyRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TotalAttemptCount",
                table: "IdempotencyRequests",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "IdempotencyRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRequests_State_InterventionRequired_NextRetryAtUtc_OperationId",
                table: "IdempotencyRequests",
                columns: new[] { "State", "InterventionRequired", "NextRetryAtUtc", "OperationId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_IdempotencyRequests_Cancellation",
                table: "IdempotencyRequests",
                sql: "([CancelIntentId] IS NULL AND [CancelRequestedByUserId] IS NULL AND [CancelReason] IS NULL) OR ([CancelIntentId] IS NOT NULL AND [CancelRequestedByUserId] IS NOT NULL AND LEN([CancelReason])>0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IdempotencyRequests_D3Counters",
                table: "IdempotencyRequests",
                sql: "[AttemptCount]>=0 AND [TotalAttemptCount]>=[AttemptCount] AND [FencingToken]>=0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IdempotencyRequests_Lease",
                table: "IdempotencyRequests",
                sql: "([LeaseOwner] IS NULL AND [LeaseUntilUtc] IS NULL) OR ([LeaseOwner] IS NOT NULL AND [LeaseUntilUtc] IS NOT NULL AND [CurrentAttemptId] IS NOT NULL AND [FencingToken]>0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS(SELECT 1 FROM dbo.IdempotencyRequests WHERE FencingToken>0 OR TotalAttemptCount>0
                    OR CancelIntentId IS NOT NULL OR CurrentAttemptId IS NOT NULL)
                    THROW 51039,'Rollback blocked: Incident cancellation protocol data exists.',1;
                """);
            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRequests_State_InterventionRequired_NextRetryAtUtc_OperationId",
                table: "IdempotencyRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IdempotencyRequests_Cancellation",
                table: "IdempotencyRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IdempotencyRequests_D3Counters",
                table: "IdempotencyRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IdempotencyRequests_Lease",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "CancelIntentId",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "CancelRequestedByUserId",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "CurrentAttemptId",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "FencingToken",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "InterventionRequired",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "LastAttemptAtUtc",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "LeaseOwner",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "LeaseUntilUtc",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "NextRetryAtUtc",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "TotalAttemptCount",
                table: "IdempotencyRequests");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "IdempotencyRequests");
        }
    }
}
