using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssessmentRollEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssessmentRollEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedFormId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxDeclarationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Page = table.Column<int>(type: "integer", nullable: false),
                    Line = table.Column<int>(type: "integer", nullable: false),
                    EnteredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EnteredBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentRollEntries", x => x.Id);
                    table.CheckConstraint("CK_AssessmentRollEntries_Kind", "\"Kind\" IN ('AssessmentRollTaxable', 'AssessmentRollExempt')");
                    table.CheckConstraint("CK_AssessmentRollEntries_PageLine", "\"Page\" >= 1 AND \"Line\" >= 1");
                    table.ForeignKey(
                        name: "FK_AssessmentRollEntries_IssuedForms_IssuedFormId",
                        column: x => x.IssuedFormId,
                        principalTable: "IssuedForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentRollEntries_TaxDeclarations_TaxDeclarationId",
                        column: x => x.TaxDeclarationId,
                        principalTable: "TaxDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRollEntries_IssuedFormId_Page_Line",
                table: "AssessmentRollEntries",
                columns: new[] { "IssuedFormId", "Page", "Line" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRollEntries_IssuedFormId_TaxDeclarationId",
                table: "AssessmentRollEntries",
                columns: new[] { "IssuedFormId", "TaxDeclarationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRollEntries_TaxDeclarationId",
                table: "AssessmentRollEntries",
                column: "TaxDeclarationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentRollEntries");
        }
    }
}
