using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackTaxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "RulesAsOf",
                table: "Valuations",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BackTaxRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RpuId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeclaredFromYear = table.Column<int>(type: "integer", nullable: false),
                    Basis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    InitialAssessmentYear = table.Column<int>(type: "integer", nullable: false),
                    TransactionTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackTaxRuns", x => x.Id);
                    table.CheckConstraint("CK_BackTaxRuns_Years", "\"DeclaredFromYear\" <= \"InitialAssessmentYear\"");
                    table.ForeignKey(
                        name: "FK_BackTaxRuns_RealPropertyUnit_RpuId",
                        column: x => x.RpuId,
                        principalTable: "RealPropertyUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BackTaxRuns_TransactionTypes_TransactionTypeId",
                        column: x => x.TransactionTypeId,
                        principalTable: "TransactionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BackTaxPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BackTaxRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ValuationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackTaxPeriods", x => x.Id);
                    table.CheckConstraint("CK_BackTaxPeriods_Dates", "\"EndDate\" IS NULL OR \"EndDate\" >= \"StartDate\"");
                    table.ForeignKey(
                        name: "FK_BackTaxPeriods_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BackTaxPeriods_BackTaxRuns_BackTaxRunId",
                        column: x => x.BackTaxRunId,
                        principalTable: "BackTaxRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BackTaxPeriods_Valuations_ValuationId",
                        column: x => x.ValuationId,
                        principalTable: "Valuations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackTaxPeriods_AssessmentId",
                table: "BackTaxPeriods",
                column: "AssessmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackTaxPeriods_BackTaxRunId_Sequence",
                table: "BackTaxPeriods",
                columns: new[] { "BackTaxRunId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackTaxPeriods_ValuationId",
                table: "BackTaxPeriods",
                column: "ValuationId");

            migrationBuilder.CreateIndex(
                name: "IX_BackTaxRuns_RpuId",
                table: "BackTaxRuns",
                column: "RpuId");

            migrationBuilder.CreateIndex(
                name: "IX_BackTaxRuns_TransactionTypeId",
                table: "BackTaxRuns",
                column: "TransactionTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackTaxPeriods");

            migrationBuilder.DropTable(
                name: "BackTaxRuns");

            migrationBuilder.DropColumn(
                name: "RulesAsOf",
                table: "Valuations");
        }
    }
}
