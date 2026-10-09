using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReferenceForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_NoticeOfAssessmentItems_RpuId",
                table: "NoticeOfAssessmentItems",
                column: "RpuId");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeOfAssessmentItems_TaxDeclarationId",
                table: "NoticeOfAssessmentItems",
                column: "TaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRecords_SignerOfficeId",
                table: "ApprovalRecords",
                column: "SignerOfficeId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRecords_UserId",
                table: "ApprovalRecords",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalRecords_AppUsers_UserId",
                table: "ApprovalRecords",
                column: "UserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalRecords_Offices_SignerOfficeId",
                table: "ApprovalRecords",
                column: "SignerOfficeId",
                principalTable: "Offices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NoticeOfAssessmentItems_RealPropertyUnit_RpuId",
                table: "NoticeOfAssessmentItems",
                column: "RpuId",
                principalTable: "RealPropertyUnit",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NoticeOfAssessmentItems_TaxDeclarations_TaxDeclarationId",
                table: "NoticeOfAssessmentItems",
                column: "TaxDeclarationId",
                principalTable: "TaxDeclarations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalRecords_AppUsers_UserId",
                table: "ApprovalRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalRecords_Offices_SignerOfficeId",
                table: "ApprovalRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_NoticeOfAssessmentItems_RealPropertyUnit_RpuId",
                table: "NoticeOfAssessmentItems");

            migrationBuilder.DropForeignKey(
                name: "FK_NoticeOfAssessmentItems_TaxDeclarations_TaxDeclarationId",
                table: "NoticeOfAssessmentItems");

            migrationBuilder.DropIndex(
                name: "IX_NoticeOfAssessmentItems_RpuId",
                table: "NoticeOfAssessmentItems");

            migrationBuilder.DropIndex(
                name: "IX_NoticeOfAssessmentItems_TaxDeclarationId",
                table: "NoticeOfAssessmentItems");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalRecords_SignerOfficeId",
                table: "ApprovalRecords");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalRecords_UserId",
                table: "ApprovalRecords");
        }
    }
}
