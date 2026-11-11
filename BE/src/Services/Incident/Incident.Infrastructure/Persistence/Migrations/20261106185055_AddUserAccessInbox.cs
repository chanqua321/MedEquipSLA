using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Incident.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAccessInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AssignedAuthorizationVersion",
                table: "Incidents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.InboxMessages) OR EXISTS (SELECT 1 FROM dbo.Incidents WHERE AssignedAuthorizationVersion > 0) THROW 51074, 'Access reconciliation history exists; rollback refused.', 1;");
            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "AssignedAuthorizationVersion",
                table: "Incidents");
        }
    }
}
