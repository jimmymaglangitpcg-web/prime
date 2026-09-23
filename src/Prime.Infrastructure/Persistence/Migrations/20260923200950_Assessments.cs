using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Assessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RpuId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValuationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentYear = table.Column<int>(type: "integer", nullable: false),
                    MarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AssessmentLevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentPercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    AssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PreviousAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevisionReference = table.Column<Guid>(type: "uuid", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assessments_AssessmentLevels_AssessmentLevelId",
                        column: x => x.AssessmentLevelId,
                        principalTable: "AssessmentLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assessments_Assessments_PreviousAssessmentId",
                        column: x => x.PreviousAssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assessments_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assessments_RealPropertyUnit_RpuId",
                        column: x => x.RpuId,
                        principalTable: "RealPropertyUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assessments_Valuations_ValuationId",
                        column: x => x.ValuationId,
                        principalTable: "Valuations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_AssessmentLevelId",
                table: "Assessments",
                column: "AssessmentLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_AssessmentYear",
                table: "Assessments",
                column: "AssessmentYear");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_PreviousAssessmentId",
                table: "Assessments",
                column: "PreviousAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_PropertyId",
                table: "Assessments",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_RevisionReference",
                table: "Assessments",
                column: "RevisionReference");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_RpuId_EffectiveDate",
                table: "Assessments",
                columns: new[] { "RpuId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_ValuationId",
                table: "Assessments",
                column: "ValuationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assessments");
        }
    }
}
