using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RevenueImpactStudies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RevenueImpactStudies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SmvSimulationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    ReferenceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualCollection = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Discounts = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CollectionSource = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IncludeAllTaxableUnits = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevenueImpactStudies", x => x.Id);
                    table.CheckConstraint("CK_RevenueImpactStudies_Collection", "(\"ActualCollection\" IS NULL) = (\"Discounts\" IS NULL) AND COALESCE(\"ActualCollection\", 0) >= 0 AND COALESCE(\"Discounts\", 0) >= 0");
                    table.ForeignKey(
                        name: "FK_RevenueImpactStudies_SmvSimulationRuns_SmvSimulationRunId",
                        column: x => x.SmvSimulationRunId,
                        principalTable: "SmvSimulationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SmvSimulationResultLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvSimulationResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Taxable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmvSimulationResultLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmvSimulationResultLines_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSimulationResultLines_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSimulationResultLines_SmvSimulationResults_SmvSimulation~",
                        column: x => x.SmvSimulationResultId,
                        principalTable: "SmvSimulationResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RevenueImpactOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevenueImpactStudyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevenueImpactOptions", x => x.Id);
                    table.CheckConstraint("CK_RevenueImpactOptions_Rate", "\"RatePercent\" >= 0 AND \"RatePercent\" <= 100");
                    table.ForeignKey(
                        name: "FK_RevenueImpactOptions_RevenueImpactStudies_RevenueImpactStud~",
                        column: x => x.RevenueImpactStudyId,
                        principalTable: "RevenueImpactStudies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RevenueImpactRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevenueImpactStudyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    Source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevenueImpactRates", x => x.Id);
                    table.CheckConstraint("CK_RevenueImpactRates_Rate", "\"RatePercent\" >= 0 AND \"RatePercent\" <= 100");
                    table.ForeignKey(
                        name: "FK_RevenueImpactRates_RevenueImpactStudies_RevenueImpactStudyId",
                        column: x => x.RevenueImpactStudyId,
                        principalTable: "RevenueImpactStudies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RevenueImpactOptionLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevenueImpactOptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LowerValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UpperValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevenueImpactOptionLevels", x => x.Id);
                    table.CheckConstraint("CK_RevenueImpactOptionLevels_Values", "\"Percent\" >= 0 AND \"Percent\" <= 100 AND \"LowerValue\" >= 0 AND (\"UpperValue\" IS NULL OR \"UpperValue\" > \"LowerValue\")");
                    table.ForeignKey(
                        name: "FK_RevenueImpactOptionLevels_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RevenueImpactOptionLevels_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RevenueImpactOptionLevels_RevenueImpactOptions_RevenueImpac~",
                        column: x => x.RevenueImpactOptionId,
                        principalTable: "RevenueImpactOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RevenueImpactOptionLevels_ActualUseId",
                table: "RevenueImpactOptionLevels",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueImpactOptionLevels_ClassificationId",
                table: "RevenueImpactOptionLevels",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueImpactOptionLevels_RevenueImpactOptionId",
                table: "RevenueImpactOptionLevels",
                column: "RevenueImpactOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueImpactOptions_RevenueImpactStudyId_Sequence",
                table: "RevenueImpactOptions",
                columns: new[] { "RevenueImpactStudyId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevenueImpactRates_RevenueImpactStudyId",
                table: "RevenueImpactRates",
                column: "RevenueImpactStudyId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueImpactStudies_SmvSimulationRunId",
                table: "RevenueImpactStudies",
                column: "SmvSimulationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationResultLines_ActualUseId",
                table: "SmvSimulationResultLines",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationResultLines_ClassificationId",
                table: "SmvSimulationResultLines",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationResultLines_SmvSimulationResultId_Sequence",
                table: "SmvSimulationResultLines",
                columns: new[] { "SmvSimulationResultId", "Sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RevenueImpactOptionLevels");

            migrationBuilder.DropTable(
                name: "RevenueImpactRates");

            migrationBuilder.DropTable(
                name: "SmvSimulationResultLines");

            migrationBuilder.DropTable(
                name: "RevenueImpactOptions");

            migrationBuilder.DropTable(
                name: "RevenueImpactStudies");
        }
    }
}
