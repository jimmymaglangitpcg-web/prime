using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssessmentLevelCeilings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssessmentLevelCeilings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PropertyTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LowerValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UpperValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaximumPercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentLevelCeilings", x => x.Id);
                    table.CheckConstraint("CK_AssessmentLevelCeilings_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_AssessmentLevelCeilings_Bracket", "\"LowerValue\" >= 0 AND (\"UpperValue\" IS NULL OR \"UpperValue\" > \"LowerValue\")");
                    table.CheckConstraint("CK_AssessmentLevelCeilings_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
                    table.CheckConstraint("CK_AssessmentLevelCeilings_Percentage", "\"MaximumPercentage\" > 0 AND \"MaximumPercentage\" <= 100");
                    table.ForeignKey(
                        name: "FK_AssessmentLevelCeilings_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentLevelCeilings_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentLevelCeilings_PropertyTypes_PropertyTypeId",
                        column: x => x.PropertyTypeId,
                        principalTable: "PropertyTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentLevelCeilings_ActualUseId",
                table: "AssessmentLevelCeilings",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentLevelCeilings_ClassificationId",
                table: "AssessmentLevelCeilings",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentLevelCeilings_Code_EffectiveDate",
                table: "AssessmentLevelCeilings",
                columns: new[] { "Code", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentLevelCeilings_PropertyTypeId_EffectiveDate",
                table: "AssessmentLevelCeilings",
                columns: new[] { "PropertyTypeId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentLevelCeilings_Status_EffectiveDate",
                table: "AssessmentLevelCeilings",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UX_AssessmentLevelCeilings_OpenApproved",
                table: "AssessmentLevelCeilings",
                column: "Code",
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentLevelCeilings");
        }
    }
}
