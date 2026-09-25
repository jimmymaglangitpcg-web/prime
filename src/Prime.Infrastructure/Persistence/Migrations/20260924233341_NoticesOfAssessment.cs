using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NoticesOfAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NoticesOfAssessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RpuId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxDeclarationId = table.Column<Guid>(type: "uuid", nullable: true),
                    NoticeNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PreviousAssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AssessmentYear = table.Column<int>(type: "integer", nullable: false),
                    AssessmentEffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AddresseeNames = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AddresseeAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IssuePeriodDays = table.Column<int>(type: "integer", nullable: false),
                    IssueDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AppealPeriodDays = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IssuedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ServedTo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ProofReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ServiceNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ServiceRecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ServiceRecordedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AppealDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticesOfAssessment", x => x.Id);
                    table.CheckConstraint("CK_NoticesOfAssessment_Cancelled", "(\"Status\" = 'Cancelled') = (\"CancelledAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_NoticesOfAssessment_Issued", "(\"Status\" IN ('Issued', 'Served')) <= (\"IssuedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_NoticesOfAssessment_Periods", "\"IssuePeriodDays\" > 0 AND \"AppealPeriodDays\" > 0");
                    table.CheckConstraint("CK_NoticesOfAssessment_Served", "(\"Status\" = 'Served') = (\"ReceivedDate\" IS NOT NULL AND \"ServiceMode\" IS NOT NULL AND \"ProofReference\" IS NOT NULL AND \"AppealDeadline\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_NoticesOfAssessment_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoticesOfAssessment_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoticesOfAssessment_RealPropertyUnit_RpuId",
                        column: x => x.RpuId,
                        principalTable: "RealPropertyUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoticesOfAssessment_TaxDeclarations_TaxDeclarationId",
                        column: x => x.TaxDeclarationId,
                        principalTable: "TaxDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NoticesOfAssessment_NoticeNumber",
                table: "NoticesOfAssessment",
                column: "NoticeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoticesOfAssessment_PropertyId_Status",
                table: "NoticesOfAssessment",
                columns: new[] { "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_NoticesOfAssessment_RpuId",
                table: "NoticesOfAssessment",
                column: "RpuId");

            migrationBuilder.CreateIndex(
                name: "IX_NoticesOfAssessment_TaxDeclarationId",
                table: "NoticesOfAssessment",
                column: "TaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "UX_NoticesOfAssessment_Assessment_Live",
                table: "NoticesOfAssessment",
                column: "AssessmentId",
                unique: true,
                filter: "\"Status\" <> 'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NoticesOfAssessment");
        }
    }
}
