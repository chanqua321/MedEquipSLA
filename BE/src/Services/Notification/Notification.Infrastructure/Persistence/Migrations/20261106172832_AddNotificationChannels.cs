using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEmailRequested",
                table: "Notifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsInAppVisible",
                table: "Notifications",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RealtimeNotifiedAtUtc",
                table: "Notifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IsInAppVisible_RealtimeNotifiedAtUtc_Id",
                table: "Notifications",
                columns: new[] { "IsInAppVisible", "RealtimeNotifiedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS(SELECT 1 FROM dbo.Notifications WHERE IsInAppVisible=0 OR IsEmailRequested=1 OR RealtimeNotifiedAtUtc IS NOT NULL)
                    THROW 51042,'Rollback blocked: notification channel decisions or realtime delivery metadata exist.',1;
                """);
            migrationBuilder.DropIndex(
                name: "IX_Notifications_IsInAppVisible_RealtimeNotifiedAtUtc_Id",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsEmailRequested",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsInAppVisible",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RealtimeNotifiedAtUtc",
                table: "Notifications");
        }
    }
}
