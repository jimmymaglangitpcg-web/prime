using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SmvSimulations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmvSimulationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvId = table.Column<Guid>(type: "uuid", nullable: false),
                    AsOf = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessedCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    StartedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmvSimulationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmvSimulationRuns_Smvs_SmvId",
                        column: x => x.SmvId,
                        principalTable: "Smvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SmvSimulationResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvSimulationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RpuId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RpuNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RpuType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CurrentAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentMarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrentAssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SimulatedClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SimulatedMarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SimulatedAssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SimulatedTaxableAssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmvSimulationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmvSimulationResults_Assessments_CurrentAssessmentId",
                        column: x => x.CurrentAssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSimulationResults_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSimulationResults_Classifications_CurrentClassificationId",
                        column: x => x.CurrentClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSimulationResults_Classifications_SimulatedClassificatio~",
                        column: x => x.SimulatedClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSimulationResults_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSimulationResults_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSimulationResults_RealPropertyUnit_RpuId",
                        column: x => x.RpuId,
                        principalTable: "RealPropertyUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSimulationResults_SmvSimulationRuns_SmvSimulationRunId",
                        column: x => x.SmvSimulationRunId,
                        principalTable: "SmvSimulationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SmvSimulationScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvSimulationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmvSimulationScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmvSimulationScopes_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSimulationScopes_SmvSimulationRuns_SmvSimulationRunId",
                        column: x => x.SmvSimulationRunId,
                        principalTable: "SmvSimulationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationResults_BarangayId",
                table: "SmvSimulationResults",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationResults_CurrentAssessmentId",
                table: "SmvSimulationResults",
                column: "CurrentAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationResults_CurrentClassificationId",
                table: "SmvSimulationResults",
                column: "CurrentClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationResults_MunicipalityId",
                table: "SmvSimulationResults",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationResults_PropertyId",
                table: "SmvSimulationResults",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationResults_RpuId",
                table: "SmvSimulationResults",
                column: "RpuId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationResults_SimulatedClassificationId",
                table: "SmvSimulationResults",
                column: "SimulatedClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationResults_SmvSimulationRunId_BarangayId",
                table: "SmvSimulationResults",
                columns: new[] { "SmvSimulationRunId", "BarangayId" });

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationResults_SmvSimulationRunId_Pin",
                table: "SmvSimulationResults",
                columns: new[] { "SmvSimulationRunId", "Pin" });

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationResults_SmvSimulationRunId_RpuId",
                table: "SmvSimulationResults",
                columns: new[] { "SmvSimulationRunId", "RpuId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationRuns_SmvId",
                table: "SmvSimulationRuns",
                column: "SmvId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationScopes_MunicipalityId",
                table: "SmvSimulationScopes",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSimulationScopes_SmvSimulationRunId_MunicipalityId",
                table: "SmvSimulationScopes",
                columns: new[] { "SmvSimulationRunId", "MunicipalityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmvSimulationResults");

            migrationBuilder.DropTable(
                name: "SmvSimulationScopes");

            migrationBuilder.DropTable(
                name: "SmvSimulationRuns");
        }
    }
}
