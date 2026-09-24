using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MachineryReplacementCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBrandNew",
                table: "MachineryUnits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ReplacementCost",
                table: "MachineryUnits",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MachineryUnits_BrandNewNoReplacementCost",
                table: "MachineryUnits",
                sql: "NOT \"IsBrandNew\" OR \"ReplacementCost\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MachineryUnits_ReplacementCost",
                table: "MachineryUnits",
                sql: "\"ReplacementCost\" IS NULL OR \"ReplacementCost\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MachineryUnits_BrandNewNoReplacementCost",
                table: "MachineryUnits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MachineryUnits_ReplacementCost",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "IsBrandNew",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "ReplacementCost",
                table: "MachineryUnits");
        }
    }
}
