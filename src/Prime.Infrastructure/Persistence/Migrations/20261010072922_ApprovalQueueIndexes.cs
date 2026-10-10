using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalQueueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_PendingReview_CreatedAt",
                table: "TaxDeclarations",
                column: "CreatedAt",
                filter: "\"Status\" = 'PendingReview'");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_PendingReview_CreatedAt",
                table: "Assessments",
                column: "CreatedAt",
                filter: "\"Status\" = 'PendingReview'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaxDeclarations_PendingReview_CreatedAt",
                table: "TaxDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_Assessments_PendingReview_CreatedAt",
                table: "Assessments");
        }
    }
}
