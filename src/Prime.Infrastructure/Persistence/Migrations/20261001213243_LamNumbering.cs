using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LamNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NoticesOfAssessment_NoticeNumber",
                table: "NoticesOfAssessment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Barangay_PinIndexNumber",
                table: "Barangays");

            migrationBuilder.AddColumn<long>(
                name: "AssessmentCount",
                table: "TaxDeclarations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoticesOfAssessment_NoticeNumber",
                table: "NoticesOfAssessment",
                column: "NoticeNumber");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Barangay_PinIndexNumber",
                table: "Barangays",
                sql: "\"PinIndexNumber\" ~ '^[0-9]{3,4}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NoticesOfAssessment_NoticeNumber",
                table: "NoticesOfAssessment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Barangay_PinIndexNumber",
                table: "Barangays");

            migrationBuilder.DropColumn(
                name: "AssessmentCount",
                table: "TaxDeclarations");

            migrationBuilder.CreateIndex(
                name: "IX_NoticesOfAssessment_NoticeNumber",
                table: "NoticesOfAssessment",
                column: "NoticeNumber",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Barangay_PinIndexNumber",
                table: "Barangays",
                sql: "\"PinIndexNumber\" ~ '^[0-9]{4}$'");
        }
    }
}
