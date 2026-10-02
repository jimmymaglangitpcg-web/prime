using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnnotationCarryOver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CarriedFromAnnotationId",
                table: "TaxDeclarationAnnotations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CarriesOver",
                table: "AnnotationTypes",
                type: "boolean",
                nullable: false,
                // Existing types carry over (docs/analysis/records-and-forms.md §4.4, Q7).
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarationAnnotations_CarriedFromAnnotationId",
                table: "TaxDeclarationAnnotations",
                column: "CarriedFromAnnotationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarationAnnotations_TaxDeclarationId_CarriedFromAnnot~",
                table: "TaxDeclarationAnnotations",
                columns: new[] { "TaxDeclarationId", "CarriedFromAnnotationId" },
                unique: true,
                filter: "\"CarriedFromAnnotationId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_TaxDeclarationAnnotations_TaxDeclarationAnnotations_Carried~",
                table: "TaxDeclarationAnnotations",
                column: "CarriedFromAnnotationId",
                principalTable: "TaxDeclarationAnnotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaxDeclarationAnnotations_TaxDeclarationAnnotations_Carried~",
                table: "TaxDeclarationAnnotations");

            migrationBuilder.DropIndex(
                name: "IX_TaxDeclarationAnnotations_CarriedFromAnnotationId",
                table: "TaxDeclarationAnnotations");

            migrationBuilder.DropIndex(
                name: "IX_TaxDeclarationAnnotations_TaxDeclarationId_CarriedFromAnnot~",
                table: "TaxDeclarationAnnotations");

            migrationBuilder.DropColumn(
                name: "CarriedFromAnnotationId",
                table: "TaxDeclarationAnnotations");

            migrationBuilder.DropColumn(
                name: "CarriesOver",
                table: "AnnotationTypes");
        }
    }
}
