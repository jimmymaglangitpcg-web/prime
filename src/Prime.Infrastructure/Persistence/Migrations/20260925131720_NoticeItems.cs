using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NoticeItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AddresseeTaxpayerId",
                table: "NoticesOfAssessment",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NoticeOfAssessmentItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NoticeOfAssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RpuId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxDeclarationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PreviousAssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AssessmentYear = table.Column<int>(type: "integer", nullable: false),
                    AssessmentEffectiveDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeOfAssessmentItems", x => x.Id);
                    table.CheckConstraint("CK_NoticeOfAssessmentItems_Sequence", "\"Sequence\" >= 1");
                    table.ForeignKey(
                        name: "FK_NoticeOfAssessmentItems_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoticeOfAssessmentItems_NoticesOfAssessment_NoticeOfAssessm~",
                        column: x => x.NoticeOfAssessmentId,
                        principalTable: "NoticesOfAssessment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoticeOfAssessmentItems_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NoticesOfAssessment_AddresseeTaxpayerId",
                table: "NoticesOfAssessment",
                column: "AddresseeTaxpayerId");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeOfAssessmentItems_AssessmentId",
                table: "NoticeOfAssessmentItems",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeOfAssessmentItems_NoticeOfAssessmentId_Sequence",
                table: "NoticeOfAssessmentItems",
                columns: new[] { "NoticeOfAssessmentId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoticeOfAssessmentItems_PropertyId",
                table: "NoticeOfAssessmentItems",
                column: "PropertyId");

            migrationBuilder.AddForeignKey(
                name: "FK_NoticesOfAssessment_Taxpayers_AddresseeTaxpayerId",
                table: "NoticesOfAssessment",
                column: "AddresseeTaxpayerId",
                principalTable: "Taxpayers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            // Every existing notice lists its one assessment (docs/analysis/mrpaao-forms-model.md §14). No value changes.
            migrationBuilder.Sql("""
                INSERT INTO "NoticeOfAssessmentItems" ("Id", "NoticeOfAssessmentId", "Sequence", "PropertyId", "RpuId", "AssessmentId",
                    "TaxDeclarationId", "Reason", "PreviousAssessedValue", "AssessedValue", "MarketValue", "AssessmentYear", "AssessmentEffectiveDate")
                SELECT gen_random_uuid(), n."Id", 1, n."PropertyId", n."RpuId", n."AssessmentId", n."TaxDeclarationId", n."Reason",
                    n."PreviousAssessedValue", n."AssessedValue", n."MarketValue", n."AssessmentYear", n."AssessmentEffectiveDate"
                FROM "NoticesOfAssessment" n
                WHERE NOT EXISTS (SELECT 1 FROM "NoticeOfAssessmentItems" x WHERE x."NoticeOfAssessmentId" = n."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NoticesOfAssessment_Taxpayers_AddresseeTaxpayerId",
                table: "NoticesOfAssessment");

            migrationBuilder.DropTable(
                name: "NoticeOfAssessmentItems");

            migrationBuilder.DropIndex(
                name: "IX_NoticesOfAssessment_AddresseeTaxpayerId",
                table: "NoticesOfAssessment");

            migrationBuilder.DropColumn(
                name: "AddresseeTaxpayerId",
                table: "NoticesOfAssessment");
        }
    }
}
