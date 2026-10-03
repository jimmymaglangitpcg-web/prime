using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LineTaxability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReassessmentId",
                table: "PropertyExemptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyExemptionId",
                table: "AssessmentLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Taxability",
                table: "AssessmentLines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Taxable");

            migrationBuilder.AddColumn<string>(
                name: "TaxabilityNote",
                table: "AssessmentLines",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyExemptions_ReassessmentId",
                table: "PropertyExemptions",
                column: "ReassessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentLines_PropertyExemptionId",
                table: "AssessmentLines",
                column: "PropertyExemptionId");

            // Existing lines take their TD's taxability (docs/analysis/assessment-listing-exemptions.md Q15): a line of an
            // assessment declared exempt becomes exempt, with no exemption record invented for it.
            migrationBuilder.Sql("""
                UPDATE "AssessmentLines" l SET "Taxability" = 'Exempt'
                WHERE EXISTS (SELECT 1 FROM "TaxDeclarations" td WHERE td."AssessmentId" = l."AssessmentId" AND td."Taxability" = 'Exempt');
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssessmentLines_Taxability",
                table: "AssessmentLines",
                sql: "\"Taxability\" IN ('Taxable', 'Exempt')");

            migrationBuilder.AddForeignKey(
                name: "FK_AssessmentLines_PropertyExemptions_PropertyExemptionId",
                table: "AssessmentLines",
                column: "PropertyExemptionId",
                principalTable: "PropertyExemptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyExemptions_Assessments_ReassessmentId",
                table: "PropertyExemptions",
                column: "ReassessmentId",
                principalTable: "Assessments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssessmentLines_PropertyExemptions_PropertyExemptionId",
                table: "AssessmentLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyExemptions_Assessments_ReassessmentId",
                table: "PropertyExemptions");

            migrationBuilder.DropIndex(
                name: "IX_PropertyExemptions_ReassessmentId",
                table: "PropertyExemptions");

            migrationBuilder.DropIndex(
                name: "IX_AssessmentLines_PropertyExemptionId",
                table: "AssessmentLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssessmentLines_Taxability",
                table: "AssessmentLines");

            migrationBuilder.DropColumn(
                name: "ReassessmentId",
                table: "PropertyExemptions");

            migrationBuilder.DropColumn(
                name: "PropertyExemptionId",
                table: "AssessmentLines");

            migrationBuilder.DropColumn(
                name: "Taxability",
                table: "AssessmentLines");

            migrationBuilder.DropColumn(
                name: "TaxabilityNote",
                table: "AssessmentLines");
        }
    }
}
