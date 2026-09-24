using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaxBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaxBills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RpuId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxDeclarationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxYear = table.Column<int>(type: "integer", nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RulesAsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscountStackingAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SupersededByBillId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxBills", x => x.Id);
                    table.CheckConstraint("CK_TaxBills_AssessedValue", "\"AssessedValue\" >= 0");
                    table.CheckConstraint("CK_TaxBills_Cancelled", "(\"Status\" = 'Cancelled') = (\"CancelledAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_TaxBills_Posted", "(\"Status\" = 'Draft' AND \"PostedAt\" IS NULL) OR (\"Status\" = 'Posted' AND \"PostedAt\" IS NOT NULL) OR \"Status\" = 'Cancelled'");
                    table.ForeignKey(
                        name: "FK_TaxBills_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxBills_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxBills_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxBills_RealPropertyUnit_RpuId",
                        column: x => x.RpuId,
                        principalTable: "RealPropertyUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxBills_TaxBills_SupersededByBillId",
                        column: x => x.SupersededByBillId,
                        principalTable: "TaxBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxBills_TaxDeclarations_TaxDeclarationId",
                        column: x => x.TaxDeclarationId,
                        principalTable: "TaxDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxBillDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxBillId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    InstallmentSequence = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TaxTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Component = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Months = table.Column<int>(type: "integer", nullable: true),
                    Explanation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxBillDetails", x => x.Id);
                    table.CheckConstraint("CK_TaxBillDetails_Installment", "\"InstallmentSequence\" >= 1");
                    table.CheckConstraint("CK_TaxBillDetails_Sign", "(\"Component\" = 'Discount') = (\"Amount\" < 0) OR \"Amount\" = 0");
                    table.ForeignKey(
                        name: "FK_TaxBillDetails_TaxBills_TaxBillId",
                        column: x => x.TaxBillId,
                        principalTable: "TaxBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxBillDetails_TaxTypes_TaxTypeId",
                        column: x => x.TaxTypeId,
                        principalTable: "TaxTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxBillTaxTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxBillId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxRateId = table.Column<Guid>(type: "uuid", nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    ComputedAnnualTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CapRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CapBaselineTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CapLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AnnualTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxBillTaxTypes", x => x.Id);
                    table.CheckConstraint("CK_TaxBillTaxTypes_AnnualTax", "\"AnnualTax\" >= 0 AND \"AnnualTax\" <= \"ComputedAnnualTax\"");
                    table.ForeignKey(
                        name: "FK_TaxBillTaxTypes_TaxBills_TaxBillId",
                        column: x => x.TaxBillId,
                        principalTable: "TaxBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxBillTaxTypes_TaxIncreaseCapRules_CapRuleId",
                        column: x => x.CapRuleId,
                        principalTable: "TaxIncreaseCapRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxBillTaxTypes_TaxRates_TaxRateId",
                        column: x => x.TaxRateId,
                        principalTable: "TaxRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxBillTaxTypes_TaxTypes_TaxTypeId",
                        column: x => x.TaxTypeId,
                        principalTable: "TaxTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxBillDetails_TaxBillId_LineNumber",
                table: "TaxBillDetails",
                columns: new[] { "TaxBillId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxBillDetails_TaxTypeId",
                table: "TaxBillDetails",
                column: "TaxTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxBills_AssessmentId",
                table: "TaxBills",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxBills_ClassificationId",
                table: "TaxBills",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxBills_PropertyId_TaxYear",
                table: "TaxBills",
                columns: new[] { "PropertyId", "TaxYear" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxBills_SupersededByBillId",
                table: "TaxBills",
                column: "SupersededByBillId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxBills_TaxDeclarationId",
                table: "TaxBills",
                column: "TaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "UX_TaxBills_Rpu_TaxYear_AsOf_Live",
                table: "TaxBills",
                columns: new[] { "RpuId", "TaxYear", "AsOfDate" },
                unique: true,
                filter: "\"Status\" <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "UX_TaxBills_Rpu_TaxYear_Posted",
                table: "TaxBills",
                columns: new[] { "RpuId", "TaxYear" },
                unique: true,
                filter: "\"Status\" = 'Posted'");

            migrationBuilder.CreateIndex(
                name: "IX_TaxBillTaxTypes_CapRuleId",
                table: "TaxBillTaxTypes",
                column: "CapRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxBillTaxTypes_TaxBillId_TaxTypeId",
                table: "TaxBillTaxTypes",
                columns: new[] { "TaxBillId", "TaxTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxBillTaxTypes_TaxRateId",
                table: "TaxBillTaxTypes",
                column: "TaxRateId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxBillTaxTypes_TaxTypeId",
                table: "TaxBillTaxTypes",
                column: "TaxTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaxBillDetails");

            migrationBuilder.DropTable(
                name: "TaxBillTaxTypes");

            migrationBuilder.DropTable(
                name: "TaxBills");
        }
    }
}
