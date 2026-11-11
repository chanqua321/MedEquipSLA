using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maintenance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderOperationPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "OperationPhoto",
                table: "WorkOrderAttempts",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationPhotoContentType",
                table: "WorkOrderAttempts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkOrderAttempts_Photo",
                table: "WorkOrderAttempts",
                sql: "([OperationPhoto] IS NULL AND [OperationPhotoContentType] IS NULL) OR ([OperationPhoto] IS NOT NULL AND [OperationPhotoContentType] IS NOT NULL AND DATALENGTH([OperationPhoto]) BETWEEN 1 AND 5242880 AND [OperationPhotoContentType] IN (N'image/png',N'image/jpeg'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS(SELECT 1 FROM dbo.WorkOrderAttempts WHERE OperationPhoto IS NOT NULL OR OperationPhotoContentType IS NOT NULL)
                    THROW 51036,'Rollback blocked: WorkOrder photo evidence exists.',1;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkOrderAttempts_Photo",
                table: "WorkOrderAttempts");

            migrationBuilder.DropColumn(
                name: "OperationPhoto",
                table: "WorkOrderAttempts");

            migrationBuilder.DropColumn(
                name: "OperationPhotoContentType",
                table: "WorkOrderAttempts");
        }
    }
}
