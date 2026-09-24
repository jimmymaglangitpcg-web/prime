using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BillingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "PaymentSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OrdinanceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrdinanceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentSchedules", x => x.Id);
                    table.CheckConstraint("CK_PaymentSchedules_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_PaymentSchedules_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
                });

            migrationBuilder.CreateTable(
                name: "TaxTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentScheduleInstallments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    DueMonth = table.Column<int>(type: "integer", nullable: false),
                    DueDay = table.Column<int>(type: "integer", nullable: false),
                    SharePercent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentScheduleInstallments", x => x.Id);
                    table.CheckConstraint("CK_PaymentScheduleInstallments_DueDay", "\"DueDay\" BETWEEN 1 AND 31");
                    table.CheckConstraint("CK_PaymentScheduleInstallments_DueMonth", "\"DueMonth\" BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_PaymentScheduleInstallments_Sequence", "\"Sequence\" >= 1");
                    table.CheckConstraint("CK_PaymentScheduleInstallments_Share", "\"SharePercent\" > 0 AND \"SharePercent\" <= 100");
                    table.ForeignKey(
                        name: "FK_PaymentScheduleInstallments_PaymentSchedules_PaymentSchedul~",
                        column: x => x.PaymentScheduleId,
                        principalTable: "PaymentSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiscountRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    CutoffMonth = table.Column<int>(type: "integer", nullable: true),
                    CutoffDay = table.Column<int>(type: "integer", nullable: true),
                    CutoffYearOffset = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OrdinanceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrdinanceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountRules", x => x.Id);
                    table.CheckConstraint("CK_DiscountRules_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_DiscountRules_Cutoff", "(\"Kind\" = 'AdvancePayment') = (\"CutoffMonth\" IS NOT NULL AND \"CutoffDay\" IS NOT NULL AND \"CutoffYearOffset\" IS NOT NULL) AND (\"Kind\" = 'AdvancePayment' OR (\"CutoffMonth\" IS NULL AND \"CutoffDay\" IS NULL AND \"CutoffYearOffset\" IS NULL))");
                    table.CheckConstraint("CK_DiscountRules_CutoffRange", "\"CutoffMonth\" IS NULL OR (\"CutoffMonth\" BETWEEN 1 AND 12 AND \"CutoffDay\" BETWEEN 1 AND 31)");
                    table.CheckConstraint("CK_DiscountRules_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
                    table.CheckConstraint("CK_DiscountRules_Rate", "\"Rate\" >= 0 AND \"Rate\" <= 100");
                    table.ForeignKey(
                        name: "FK_DiscountRules_TaxTypes_TaxTypeId",
                        column: x => x.TaxTypeId,
                        principalTable: "TaxTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InterestRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    RatePerMonth = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    MaxMonths = table.Column<int>(type: "integer", nullable: true),
                    MonthCounting = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OrdinanceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrdinanceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterestRules", x => x.Id);
                    table.CheckConstraint("CK_InterestRules_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_InterestRules_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
                    table.CheckConstraint("CK_InterestRules_MaxMonths", "\"MaxMonths\" IS NULL OR \"MaxMonths\" > 0");
                    table.CheckConstraint("CK_InterestRules_Rate", "\"RatePerMonth\" >= 0 AND \"RatePerMonth\" <= 100");
                    table.ForeignKey(
                        name: "FK_InterestRules_TaxTypes_TaxTypeId",
                        column: x => x.TaxTypeId,
                        principalTable: "TaxTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PenaltyRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    FixedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AppliesAfterDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OrdinanceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrdinanceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PenaltyRules", x => x.Id);
                    table.CheckConstraint("CK_PenaltyRules_AppliesAfterDays", "\"AppliesAfterDays\" >= 0");
                    table.CheckConstraint("CK_PenaltyRules_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_PenaltyRules_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
                    table.CheckConstraint("CK_PenaltyRules_FixedAmount", "\"FixedAmount\" IS NULL OR \"FixedAmount\" >= 0");
                    table.CheckConstraint("CK_PenaltyRules_Rate", "\"Rate\" IS NULL OR (\"Rate\" >= 0 AND \"Rate\" <= 100)");
                    table.CheckConstraint("CK_PenaltyRules_RateXorFixed", "(\"Rate\" IS NULL) <> (\"FixedAmount\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_PenaltyRules_TaxTypes_TaxTypeId",
                        column: x => x.TaxTypeId,
                        principalTable: "TaxTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxIncreaseCapRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Basis = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Baseline = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MaxIncreasePercent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OrdinanceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrdinanceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxIncreaseCapRules", x => x.Id);
                    table.CheckConstraint("CK_TaxIncreaseCapRules_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_TaxIncreaseCapRules_EndDate", "\"EndDate\" IS NOT NULL");
                    table.CheckConstraint("CK_TaxIncreaseCapRules_MaxIncreasePercent", "\"MaxIncreasePercent\" >= 0");
                    table.CheckConstraint("CK_TaxIncreaseCapRules_StatutoryBaseline", "\"Basis\" <> 'StatutoryFirstYear' OR \"Baseline\" = 'TaxBeforeSmv'");
                    table.ForeignKey(
                        name: "FK_TaxIncreaseCapRules_Smvs_SmvId",
                        column: x => x.SmvId,
                        principalTable: "Smvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxIncreaseCapRules_TaxTypes_TaxTypeId",
                        column: x => x.TaxTypeId,
                        principalTable: "TaxTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OrdinanceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrdinanceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxRates", x => x.Id);
                    table.CheckConstraint("CK_TaxRates_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_TaxRates_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
                    table.CheckConstraint("CK_TaxRates_Rate", "\"Rate\" >= 0 AND \"Rate\" <= 100");
                    table.ForeignKey(
                        name: "FK_TaxRates_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxRates_TaxTypes_TaxTypeId",
                        column: x => x.TaxTypeId,
                        principalTable: "TaxTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_Status_EffectiveDate",
                table: "DiscountRules",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_TaxTypeId",
                table: "DiscountRules",
                column: "TaxTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_DiscountRules_OpenApproved",
                table: "DiscountRules",
                columns: new[] { "Kind", "TaxTypeId" },
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_InterestRules_Status_EffectiveDate",
                table: "InterestRules",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UX_InterestRules_OpenApproved",
                table: "InterestRules",
                column: "TaxTypeId",
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentScheduleInstallments_PaymentScheduleId_Sequence",
                table: "PaymentScheduleInstallments",
                columns: new[] { "PaymentScheduleId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSchedules_Status_EffectiveDate",
                table: "PaymentSchedules",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UX_PaymentSchedules_OpenApproved",
                table: "PaymentSchedules",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyRules_Status_EffectiveDate",
                table: "PenaltyRules",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UX_PenaltyRules_OpenApproved",
                table: "PenaltyRules",
                column: "TaxTypeId",
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_TaxIncreaseCapRules_SmvId_TaxTypeId_Basis",
                table: "TaxIncreaseCapRules",
                columns: new[] { "SmvId", "TaxTypeId", "Basis" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxIncreaseCapRules_Status_EffectiveDate",
                table: "TaxIncreaseCapRules",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxIncreaseCapRules_TaxTypeId",
                table: "TaxIncreaseCapRules",
                column: "TaxTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_ClassificationId",
                table: "TaxRates",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_Status_EffectiveDate",
                table: "TaxRates",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UX_TaxRates_OpenApproved",
                table: "TaxRates",
                columns: new[] { "TaxTypeId", "ClassificationId" },
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_TaxTypes_Code",
                table: "TaxTypes",
                column: "Code",
                unique: true);

            // Approved cap windows of one scope (SmvId, TaxTypeId, Basis) never
            // overlap — the database guarantee behind the approval-time check
            // (docs/BILLING.md §3.7). A null TaxTypeId ("every tax type") is its
            // own scope, as in the NULLS NOT DISTINCT indexes above.
            migrationBuilder.Sql("""
                ALTER TABLE "TaxIncreaseCapRules" ADD CONSTRAINT "EX_TaxIncreaseCapRules_NoOverlap"
                EXCLUDE USING gist (
                    "SmvId" WITH =,
                    (COALESCE("TaxTypeId", '00000000-0000-0000-0000-000000000000'::uuid)) WITH =,
                    "Basis" WITH =,
                    daterange("EffectiveDate", "EndDate", '[]') WITH &&
                ) WHERE ("Status" = 'Approved');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscountRules");

            migrationBuilder.DropTable(
                name: "InterestRules");

            migrationBuilder.DropTable(
                name: "PaymentScheduleInstallments");

            migrationBuilder.DropTable(
                name: "PenaltyRules");

            migrationBuilder.DropTable(
                name: "TaxIncreaseCapRules");

            migrationBuilder.DropTable(
                name: "TaxRates");

            migrationBuilder.DropTable(
                name: "PaymentSchedules");

            migrationBuilder.DropTable(
                name: "TaxTypes");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
