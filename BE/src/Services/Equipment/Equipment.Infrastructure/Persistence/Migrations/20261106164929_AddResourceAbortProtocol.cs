using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Equipment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceAbortProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EquipmentUseRegistrations_Payload",
                table: "EquipmentUseRegistrations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EquipmentUseRegistrations_Payload",
                table: "EquipmentUseRegistrations",
                sql: "[RequestPayloadJson] IS NULL OR (ISJSON([RequestPayloadJson]) = 1 AND (([ResourceType] IN (N'WorkOrder',N'MaintenanceSchedule') AND [SourceService] = N'Maintenance') OR ([ResourceType]=N'Incident' AND [SourceService]=N'Incident')) AND [RequestHash] IS NOT NULL AND [FencingToken] > 0 AND [EquipmentCodeSnapshot] IS NOT NULL AND [EquipmentNameSnapshot] IS NOT NULL AND [DepartmentIdSnapshot] IS NOT NULL AND [DepartmentNameSnapshot] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS(SELECT 1 FROM dbo.EquipmentUseRegistrations WHERE ResourceType IN (N'Incident',N'MaintenanceSchedule')
                    AND RequestPayloadJson IS NOT NULL)
                    THROW 51038,'Rollback blocked: resource abort tombstones exist.',1;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_EquipmentUseRegistrations_Payload",
                table: "EquipmentUseRegistrations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EquipmentUseRegistrations_Payload",
                table: "EquipmentUseRegistrations",
                sql: "[RequestPayloadJson] IS NULL OR (ISJSON([RequestPayloadJson]) = 1 AND [ResourceType] = N'WorkOrder' AND [SourceService] = N'Maintenance' AND [RequestHash] IS NOT NULL AND [FencingToken] > 0 AND [EquipmentCodeSnapshot] IS NOT NULL AND [EquipmentNameSnapshot] IS NOT NULL AND [DepartmentIdSnapshot] IS NOT NULL AND [DepartmentNameSnapshot] IS NOT NULL)");
        }
    }
}
