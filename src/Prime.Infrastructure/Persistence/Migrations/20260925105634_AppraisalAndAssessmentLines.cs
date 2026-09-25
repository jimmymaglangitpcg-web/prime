using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppraisalAndAssessmentLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "AssessmentPercentage",
                table: "Assessments",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<Guid>(
                name: "AssessmentLevelId",
                table: "Assessments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "AssessmentLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AssessmentLevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentPercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    AssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentLines", x => x.Id);
                    table.CheckConstraint("CK_AssessmentLines_Sequence", "\"Sequence\" >= 1");
                    table.CheckConstraint("CK_AssessmentLines_Values", "\"MarketValue\" >= 0 AND \"AssessedValue\" >= 0");
                    table.ForeignKey(
                        name: "FK_AssessmentLines_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentLines_AssessmentLevels_AssessmentLevelId",
                        column: x => x.AssessmentLevelId,
                        principalTable: "AssessmentLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentLines_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentLines_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentLines_PropertyTypes_PropertyTypeId",
                        column: x => x.PropertyTypeId,
                        principalTable: "PropertyTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxBillTaxTypeLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxBillTaxTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRateId = table.Column<Guid>(type: "uuid", nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Tax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxBillTaxTypeLines", x => x.Id);
                    table.CheckConstraint("CK_TaxBillTaxTypeLines_Values", "\"AssessedValue\" >= 0 AND \"Tax\" >= 0");
                    table.ForeignKey(
                        name: "FK_TaxBillTaxTypeLines_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxBillTaxTypeLines_TaxBillTaxTypes_TaxBillTaxTypeId",
                        column: x => x.TaxBillTaxTypeId,
                        principalTable: "TaxBillTaxTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxBillTaxTypeLines_TaxRates_TaxRateId",
                        column: x => x.TaxRateId,
                        principalTable: "TaxRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ValuationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ValuationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UnitValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SmvScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                    MarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BreakdownJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValuationLines", x => x.Id);
                    table.CheckConstraint("CK_ValuationLines_MarketValue", "\"MarketValue\" >= 0");
                    table.CheckConstraint("CK_ValuationLines_Sequence", "\"Sequence\" >= 1");
                    table.ForeignKey(
                        name: "FK_ValuationLines_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ValuationLines_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ValuationLines_SmvSchedules_SmvScheduleId",
                        column: x => x.SmvScheduleId,
                        principalTable: "SmvSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ValuationLines_SubClassifications_SubClassificationId",
                        column: x => x.SubClassificationId,
                        principalTable: "SubClassifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ValuationLines_Valuations_ValuationId",
                        column: x => x.ValuationId,
                        principalTable: "Valuations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assessments_Level",
                table: "Assessments",
                sql: "(\"AssessmentLevelId\" IS NULL) = (\"AssessmentPercentage\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentLines_ActualUseId",
                table: "AssessmentLines",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentLines_AssessmentId_ClassificationId_ActualUseId",
                table: "AssessmentLines",
                columns: new[] { "AssessmentId", "ClassificationId", "ActualUseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentLines_AssessmentId_Sequence",
                table: "AssessmentLines",
                columns: new[] { "AssessmentId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentLines_AssessmentLevelId",
                table: "AssessmentLines",
                column: "AssessmentLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentLines_ClassificationId",
                table: "AssessmentLines",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentLines_PropertyTypeId",
                table: "AssessmentLines",
                column: "PropertyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxBillTaxTypeLines_ClassificationId",
                table: "TaxBillTaxTypeLines",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxBillTaxTypeLines_TaxBillTaxTypeId",
                table: "TaxBillTaxTypeLines",
                column: "TaxBillTaxTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxBillTaxTypeLines_TaxRateId",
                table: "TaxBillTaxTypeLines",
                column: "TaxRateId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationLines_ActualUseId",
                table: "ValuationLines",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationLines_ClassificationId",
                table: "ValuationLines",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationLines_SmvScheduleId",
                table: "ValuationLines",
                column: "SmvScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationLines_SubClassificationId",
                table: "ValuationLines",
                column: "SubClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationLines_ValuationId_Sequence",
                table: "ValuationLines",
                columns: new[] { "ValuationId", "Sequence" },
                unique: true);
            // Existing records get one line each, carrying the values they already hold
            // (docs/analysis/mrpaao-forms-model.md §8.5). No value changes.
            migrationBuilder.Sql("""
                INSERT INTO "ValuationLines" ("Id", "ValuationId", "Sequence", "Source", "SourceId", "ClassificationId", "SubClassificationId",
                    "ActualUseId", "Quantity", "Unit", "UnitValue", "SmvScheduleId", "MarketValue", "BreakdownJson")
                SELECT gen_random_uuid(), v."Id", 1, v."SourceType", v."SourceId", l."ClassificationId", l."SubClassificationId", l."ActualUseId",
                    COALESCE((v."BreakdownJson" ->> 'Area')::numeric, (v."BreakdownJson" ->> 'TotalFloorArea')::numeric),
                    CASE WHEN v."SourceType" = 'Land' THEN l."AreaUnit" ELSE s."Unit" END,
                    (v."BreakdownJson" ->> 'Rate')::numeric, v."SmvScheduleId", v."ComputedMarketValue", v."BreakdownJson"
                FROM "Valuations" v
                LEFT JOIN "Lands" l ON v."SourceType" = 'Land' AND l."Id" = v."SourceId"
                LEFT JOIN "SmvSchedules" s ON s."Id" = v."SmvScheduleId"
                WHERE NOT EXISTS (SELECT 1 FROM "ValuationLines" x WHERE x."ValuationId" = v."Id");
                """);
            migrationBuilder.Sql("""
                INSERT INTO "AssessmentLines" ("Id", "AssessmentId", "Sequence", "ClassificationId", "ActualUseId", "PropertyTypeId",
                    "MarketValue", "AssessmentLevelId", "AssessmentPercentage", "AssessedValue")
                SELECT gen_random_uuid(), a."Id", 1, al."ClassificationId", al."ActualUseId", al."PropertyTypeId",
                    a."MarketValue", a."AssessmentLevelId", a."AssessmentPercentage", a."AssessedValue"
                FROM "Assessments" a
                JOIN "AssessmentLevels" al ON al."Id" = a."AssessmentLevelId"
                WHERE NOT EXISTS (SELECT 1 FROM "AssessmentLines" x WHERE x."AssessmentId" = a."Id");
                """);
            migrationBuilder.Sql("""
                INSERT INTO "TaxBillTaxTypeLines" ("Id", "TaxBillTaxTypeId", "ClassificationId", "AssessedValue", "TaxRateId", "RatePercent", "Tax")
                SELECT gen_random_uuid(), t."Id", b."ClassificationId", b."AssessedValue", t."TaxRateId", t."RatePercent", t."ComputedAnnualTax"
                FROM "TaxBillTaxTypes" t
                JOIN "TaxBills" b ON b."Id" = t."TaxBillId"
                WHERE NOT EXISTS (SELECT 1 FROM "TaxBillTaxTypeLines" x WHERE x."TaxBillTaxTypeId" = t."Id");
                """);
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentLines");

            migrationBuilder.DropTable(
                name: "TaxBillTaxTypeLines");

            migrationBuilder.DropTable(
                name: "ValuationLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assessments_Level",
                table: "Assessments");

            migrationBuilder.AlterColumn<decimal>(
                name: "AssessmentPercentage",
                table: "Assessments",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,6)",
                oldPrecision: 9,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AssessmentLevelId",
                table: "Assessments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
