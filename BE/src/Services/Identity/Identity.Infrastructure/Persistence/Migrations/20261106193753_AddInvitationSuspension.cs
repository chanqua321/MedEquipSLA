using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationSuspension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInvitationSuspended",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
            // Legacy disabled/unactivated rows must not gain activation rights from a schema upgrade.
            migrationBuilder.Sql("UPDATE dbo.AspNetUsers SET IsInvitationSuspended=1 WHERE IsActive=0 AND EmailConfirmed=0 AND PasswordHash IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.AspNetUsers WHERE IsInvitationSuspended=1) THROW 51075, 'Suspended invitation state exists; rollback refused.', 1;");
            migrationBuilder.DropColumn(
                name: "IsInvitationSuspended",
                table: "AspNetUsers");
        }
    }
}
