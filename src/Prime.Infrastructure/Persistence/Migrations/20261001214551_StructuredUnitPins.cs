using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StructuredUnitPins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PinAssignments_Permanent",
                table: "PinAssignments");

            migrationBuilder.AddColumn<int>(
                name: "FloorNumber",
                table: "RealPropertyUnit",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FloorPrefix",
                table: "RealPropertyUnit",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLeasingProperty",
                table: "RealPropertyUnit",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "UnitNumber",
                table: "RealPropertyUnit",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOverWater",
                table: "Property",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "UX_RealPropertyUnit_LeasingUnit",
                table: "RealPropertyUnit",
                columns: new[] { "HostRpuId", "FloorPrefix", "FloorNumber", "UnitNumber" },
                unique: true,
                filter: "\"UnitNumber\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PinAssignments_Permanent",
                table: "PinAssignments",
                sql: "\"Kind\" <> 'Permanent' OR (\"SectionId\" IS NOT NULL AND \"ParcelNumber\" IS NOT NULL AND (\"ParcelId\" IS NOT NULL OR \"ParcelNumber\" = 0))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_RealPropertyUnit_LeasingUnit",
                table: "RealPropertyUnit");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PinAssignments_Permanent",
                table: "PinAssignments");

            migrationBuilder.DropColumn(
                name: "FloorNumber",
                table: "RealPropertyUnit");

            migrationBuilder.DropColumn(
                name: "FloorPrefix",
                table: "RealPropertyUnit");

            migrationBuilder.DropColumn(
                name: "IsLeasingProperty",
                table: "RealPropertyUnit");

            migrationBuilder.DropColumn(
                name: "UnitNumber",
                table: "RealPropertyUnit");

            migrationBuilder.DropColumn(
                name: "IsOverWater",
                table: "Property");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PinAssignments_Permanent",
                table: "PinAssignments",
                sql: "\"Kind\" <> 'Permanent' OR (\"ParcelId\" IS NOT NULL AND \"SectionId\" IS NOT NULL AND \"ParcelNumber\" IS NOT NULL)");
        }
    }
}
