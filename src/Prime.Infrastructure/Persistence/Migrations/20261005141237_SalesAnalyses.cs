using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvPreparationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalesFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    SalesTo = table.Column<DateOnly>(type: "date", nullable: true),
                    AreaUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RoundingIncrement = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RangeWidthPercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesAnalyses", x => x.Id);
                    table.CheckConstraint("CK_SalesAnalyses_Parameters", "\"RoundingIncrement\" >= 0 AND (\"RangeWidthPercent\" IS NULL OR (\"RangeWidthPercent\" > 0 AND \"RangeWidthPercent\" <= 100))");
                    table.ForeignKey(
                        name: "FK_SalesAnalyses_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesAnalyses_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesAnalyses_SmvPreparations_SmvPreparationId",
                        column: x => x.SmvPreparationId,
                        principalTable: "SmvPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SmvTimeAdjustmentFactors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvPreparationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Factor = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmvTimeAdjustmentFactors", x => x.Id);
                    table.CheckConstraint("CK_SmvTimeAdjustmentFactors_Valid", "\"PeriodFrom\" <= \"PeriodTo\" AND \"Factor\" > 0");
                    table.ForeignKey(
                        name: "FK_SmvTimeAdjustmentFactors_SmvPreparations_SmvPreparationId",
                        column: x => x.SmvPreparationId,
                        principalTable: "SmvPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesAnalysisGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ToValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SubClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdoptedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Basis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SmvScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdoptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AdoptedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesAnalysisGroups", x => x.Id);
                    table.CheckConstraint("CK_SalesAnalysisGroups_Adopted", "\"AdoptedAt\" IS NULL OR (\"SmvScheduleId\" IS NOT NULL AND \"SubClassificationId\" IS NOT NULL AND \"AdoptedValue\" > 0)");
                    table.CheckConstraint("CK_SalesAnalysisGroups_Bounds", "\"FromValue\" >= 0 AND \"FromValue\" <= \"ToValue\"");
                    table.ForeignKey(
                        name: "FK_SalesAnalysisGroups_SalesAnalyses_SalesAnalysisId",
                        column: x => x.SalesAnalysisId,
                        principalTable: "SalesAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesAnalysisGroups_SmvSchedules_SmvScheduleId",
                        column: x => x.SmvScheduleId,
                        principalTable: "SmvSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesAnalysisGroups_SubClassifications_SubClassificationId",
                        column: x => x.SubClassificationId,
                        principalTable: "SubClassifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesAnalysisSales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: true),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TaxDeclarationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Pin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Area = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TimeFactor = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    OtherAdjustmentPercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    AdjustedUnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RoundedUnitValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LeftOut = table.Column<bool>(type: "boolean", nullable: false),
                    ExclusionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesAnalysisSales", x => x.Id);
                    table.CheckConstraint("CK_SalesAnalysisSales_LeftOut", "NOT \"LeftOut\" OR \"ExclusionReason\" IS NOT NULL");
                    table.CheckConstraint("CK_SalesAnalysisSales_ValueOrReason", "(\"RoundedUnitValue\" IS NULL) = (\"ExclusionReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SalesAnalysisSales_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesAnalysisSales_MarketTransactions_MarketTransactionId",
                        column: x => x.MarketTransactionId,
                        principalTable: "MarketTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesAnalysisSales_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesAnalysisSales_SalesAnalyses_SalesAnalysisId",
                        column: x => x.SalesAnalysisId,
                        principalTable: "SalesAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesAnalysisSales_SubClassifications_SubClassificationId",
                        column: x => x.SubClassificationId,
                        principalTable: "SubClassifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesAnalysisScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesAnalysisScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesAnalysisScopes_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesAnalysisScopes_SalesAnalyses_SalesAnalysisId",
                        column: x => x.SalesAnalysisId,
                        principalTable: "SalesAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesAnalyses_ActualUseId",
                table: "SalesAnalyses",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAnalyses_ClassificationId",
                table: "SalesAnalyses",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAnalyses_SmvPreparationId_ClassificationId_ActualUseId",
                table: "SalesAnalyses",
                columns: new[] { "SmvPreparationId", "ClassificationId", "ActualUseId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_SalesAnalysisGroups_SalesAnalysisId",
                table: "SalesAnalysisGroups",
                column: "SalesAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAnalysisGroups_SmvScheduleId",
                table: "SalesAnalysisGroups",
                column: "SmvScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAnalysisGroups_SubClassificationId",
                table: "SalesAnalysisGroups",
                column: "SubClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAnalysisSales_BarangayId",
                table: "SalesAnalysisSales",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAnalysisSales_MarketTransactionId",
                table: "SalesAnalysisSales",
                column: "MarketTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAnalysisSales_MunicipalityId",
                table: "SalesAnalysisSales",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAnalysisSales_SalesAnalysisId_MarketTransactionId",
                table: "SalesAnalysisSales",
                columns: new[] { "SalesAnalysisId", "MarketTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesAnalysisSales_SubClassificationId",
                table: "SalesAnalysisSales",
                column: "SubClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAnalysisScopes_MunicipalityId",
                table: "SalesAnalysisScopes",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesAnalysisScopes_SalesAnalysisId_MunicipalityId",
                table: "SalesAnalysisScopes",
                columns: new[] { "SalesAnalysisId", "MunicipalityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmvTimeAdjustmentFactors_SmvPreparationId_PeriodFrom",
                table: "SmvTimeAdjustmentFactors",
                columns: new[] { "SmvPreparationId", "PeriodFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesAnalysisGroups");

            migrationBuilder.DropTable(
                name: "SalesAnalysisSales");

            migrationBuilder.DropTable(
                name: "SalesAnalysisScopes");

            migrationBuilder.DropTable(
                name: "SmvTimeAdjustmentFactors");

            migrationBuilder.DropTable(
                name: "SalesAnalyses");
        }
    }
}
