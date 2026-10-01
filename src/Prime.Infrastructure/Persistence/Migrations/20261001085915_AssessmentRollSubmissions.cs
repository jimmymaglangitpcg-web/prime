using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssessmentRollSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssessmentRollSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfficeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewRemarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentRollSubmissions", x => x.Id);
                    table.CheckConstraint("CK_AssessmentRollSubmissions_Month", "\"Month\" BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_AssessmentRollSubmissions_Reviewed", "(\"Status\" = 'Submitted') = (\"ReviewedAt\" IS NULL) AND (\"Status\" <> 'Returned' OR \"ReviewRemarks\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AssessmentRollSubmissions_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentRollSubmissions_Offices_OfficeId",
                        column: x => x.OfficeId,
                        principalTable: "Offices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentRollSubmissionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisterRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedFormId = table.Column<Guid>(type: "uuid", nullable: false),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EntryCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentRollSubmissionItems", x => x.Id);
                    table.CheckConstraint("CK_AssessmentRollSubmissionItems_Kind", "\"Kind\" IN ('AssessmentRollTaxable', 'AssessmentRollExempt')");
                    table.ForeignKey(
                        name: "FK_AssessmentRollSubmissionItems_AssessmentRollSubmissions_Sub~",
                        column: x => x.SubmissionId,
                        principalTable: "AssessmentRollSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentRollSubmissionItems_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentRollSubmissionItems_IssuedForms_IssuedFormId",
                        column: x => x.IssuedFormId,
                        principalTable: "IssuedForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentRollSubmissionItems_RegisterRuns_RegisterRunId",
                        column: x => x.RegisterRunId,
                        principalTable: "RegisterRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRollSubmissionItems_BarangayId",
                table: "AssessmentRollSubmissionItems",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRollSubmissionItems_IssuedFormId",
                table: "AssessmentRollSubmissionItems",
                column: "IssuedFormId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRollSubmissionItems_RegisterRunId",
                table: "AssessmentRollSubmissionItems",
                column: "RegisterRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRollSubmissionItems_SubmissionId",
                table: "AssessmentRollSubmissionItems",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRollSubmissions_OfficeId",
                table: "AssessmentRollSubmissions",
                column: "OfficeId");

            migrationBuilder.CreateIndex(
                name: "UX_AssessmentRollSubmissions_Month",
                table: "AssessmentRollSubmissions",
                columns: new[] { "MunicipalityId", "Year", "Month" },
                unique: true,
                filter: "\"Status\" <> 'Returned'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentRollSubmissionItems");

            migrationBuilder.DropTable(
                name: "AssessmentRollSubmissions");
        }
    }
}
