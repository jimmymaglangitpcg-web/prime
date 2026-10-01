using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LandAdjustmentRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepthBand",
                table: "LandStrips",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DistanceToAllWeatherRoadKm",
                table: "Lands",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DistanceToPoblacionKm",
                table: "Lands",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSubdivisionLot",
                table: "Lands",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SeparateRpuId",
                table: "LandImprovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistanceReference",
                table: "AdjustmentFactors",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuleKind",
                table: "AdjustmentFactors",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Flat"); // existing factors are flat percentages

            migrationBuilder.AddColumn<decimal>(
                name: "StandardDepth",
                table: "AdjustmentFactors",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdjustmentFactorRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdjustmentFactorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    RoadTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    OverValue = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    UpToValue = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    DepthBand = table.Column<int>(type: "integer", nullable: true),
                    Percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdjustmentFactorRows", x => x.Id);
                    table.CheckConstraint("CK_AdjustmentFactorRows_Percent", "\"Percent\" > -100 AND \"Percent\" <= 1000");
                    table.ForeignKey(
                        name: "FK_AdjustmentFactorRows_AdjustmentFactors_AdjustmentFactorId",
                        column: x => x.AdjustmentFactorId,
                        principalTable: "AdjustmentFactors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdjustmentFactorRows_RoadTypes_RoadTypeId",
                        column: x => x.RoadTypeId,
                        principalTable: "RoadTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LandStrips_DepthBand",
                table: "LandStrips",
                sql: "\"DepthBand\" IS NULL OR \"DepthBand\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_LandImprovements_SeparateRpuId",
                table: "LandImprovements",
                column: "SeparateRpuId");

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentFactorRows_AdjustmentFactorId_Sequence",
                table: "AdjustmentFactorRows",
                columns: new[] { "AdjustmentFactorId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentFactorRows_RoadTypeId",
                table: "AdjustmentFactorRows",
                column: "RoadTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_LandImprovements_RealPropertyUnit_SeparateRpuId",
                table: "LandImprovements",
                column: "SeparateRpuId",
                principalTable: "RealPropertyUnit",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LandImprovements_RealPropertyUnit_SeparateRpuId",
                table: "LandImprovements");

            migrationBuilder.DropTable(
                name: "AdjustmentFactorRows");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LandStrips_DepthBand",
                table: "LandStrips");

            migrationBuilder.DropIndex(
                name: "IX_LandImprovements_SeparateRpuId",
                table: "LandImprovements");

            migrationBuilder.DropColumn(
                name: "DepthBand",
                table: "LandStrips");

            migrationBuilder.DropColumn(
                name: "DistanceToAllWeatherRoadKm",
                table: "Lands");

            migrationBuilder.DropColumn(
                name: "DistanceToPoblacionKm",
                table: "Lands");

            migrationBuilder.DropColumn(
                name: "IsSubdivisionLot",
                table: "Lands");

            migrationBuilder.DropColumn(
                name: "SeparateRpuId",
                table: "LandImprovements");

            migrationBuilder.DropColumn(
                name: "DistanceReference",
                table: "AdjustmentFactors");

            migrationBuilder.DropColumn(
                name: "RuleKind",
                table: "AdjustmentFactors");

            migrationBuilder.DropColumn(
                name: "StandardDepth",
                table: "AdjustmentFactors");
        }
    }
}
