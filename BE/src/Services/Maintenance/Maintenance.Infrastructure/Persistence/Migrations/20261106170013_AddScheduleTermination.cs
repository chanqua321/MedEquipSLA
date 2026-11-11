using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maintenance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleTermination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EndReason",
                table: "MaintenanceSchedules",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndedAtUtc",
                table: "MaintenanceSchedules",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EndedByUserId",
                table: "MaintenanceSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RegistrationReleasedAtUtc",
                table: "MaintenanceSchedules",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_EndedAtUtc_RegistrationReleasedAtUtc_Id",
                table: "MaintenanceSchedules",
                columns: new[] { "EndedAtUtc", "RegistrationReleasedAtUtc", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaintenanceSchedules_End",
                table: "MaintenanceSchedules",
                sql: "([EndedAtUtc] IS NULL AND [EndedByUserId] IS NULL AND [EndReason] IS NULL AND [RegistrationReleasedAtUtc] IS NULL) OR ([EndedAtUtc] IS NOT NULL AND [EndedByUserId] IS NOT NULL AND [EndReason] IS NOT NULL AND LEN(LTRIM(RTRIM([EndReason])))>0 AND [IsActive]=0 AND ([RegistrationReleasedAtUtc] IS NULL OR [RegistrationReleasedAtUtc]>=[EndedAtUtc]))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS(SELECT 1 FROM dbo.MaintenanceSchedules WHERE EndedAtUtc IS NOT NULL OR RegistrationReleasedAtUtc IS NOT NULL)
                    THROW 51041,'Rollback blocked: schedule termination history exists.',1;
                """);
            migrationBuilder.DropIndex(
                name: "IX_MaintenanceSchedules_EndedAtUtc_RegistrationReleasedAtUtc_Id",
                table: "MaintenanceSchedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaintenanceSchedules_End",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "EndReason",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "EndedAtUtc",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "EndedByUserId",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "RegistrationReleasedAtUtc",
                table: "MaintenanceSchedules");
        }
    }
}
