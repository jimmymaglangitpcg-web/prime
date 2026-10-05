using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ValuationTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ValuationTestRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvId = table.Column<Guid>(type: "uuid", nullable: false),
                    AsOf = table.Column<DateOnly>(type: "date", nullable: false),
                    SalesFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    SalesTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValuationTestRuns", x => x.Id);
                    table.CheckConstraint("CK_ValuationTestRuns_SalesPeriod", "\"SalesFrom\" IS NULL OR \"SalesTo\" IS NULL OR \"SalesFrom\" <= \"SalesTo\"");
                    table.ForeignKey(
                        name: "FK_ValuationTestRuns_Smvs_SmvId",
                        column: x => x.SmvId,
                        principalTable: "Smvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ValuationTestSales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ValuationTestRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LandArea = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    LandAreaUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SmvScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                    RateUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UnitValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Ratio = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ExclusionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValuationTestSales", x => x.Id);
                    table.CheckConstraint("CK_ValuationTestSales_RatioOrReason", "(\"Ratio\" IS NULL) = (\"ExclusionReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ValuationTestSales_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ValuationTestSales_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ValuationTestSales_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ValuationTestSales_MarketTransactions_MarketTransactionId",
                        column: x => x.MarketTransactionId,
                        principalTable: "MarketTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ValuationTestSales_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ValuationTestSales_SmvSchedules_SmvScheduleId",
                        column: x => x.SmvScheduleId,
                        principalTable: "SmvSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ValuationTestSales_SubClassifications_SubClassificationId",
                        column: x => x.SubClassificationId,
                        principalTable: "SubClassifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ValuationTestSales_ValuationTestRuns_ValuationTestRunId",
                        column: x => x.ValuationTestRunId,
                        principalTable: "ValuationTestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValuationTestScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ValuationTestRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValuationTestScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValuationTestScopes_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ValuationTestScopes_ValuationTestRuns_ValuationTestRunId",
                        column: x => x.ValuationTestRunId,
                        principalTable: "ValuationTestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ValuationTestRuns_SmvId",
                table: "ValuationTestRuns",
                column: "SmvId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationTestSales_ActualUseId",
                table: "ValuationTestSales",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationTestSales_BarangayId",
                table: "ValuationTestSales",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationTestSales_ClassificationId",
                table: "ValuationTestSales",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationTestSales_MarketTransactionId",
                table: "ValuationTestSales",
                column: "MarketTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationTestSales_MunicipalityId",
                table: "ValuationTestSales",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationTestSales_SmvScheduleId",
                table: "ValuationTestSales",
                column: "SmvScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationTestSales_SubClassificationId",
                table: "ValuationTestSales",
                column: "SubClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationTestSales_ValuationTestRunId_MarketTransactionId",
                table: "ValuationTestSales",
                columns: new[] { "ValuationTestRunId", "MarketTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValuationTestScopes_MunicipalityId",
                table: "ValuationTestScopes",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationTestScopes_ValuationTestRunId_MunicipalityId",
                table: "ValuationTestScopes",
                columns: new[] { "ValuationTestRunId", "MunicipalityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ValuationTestSales");

            migrationBuilder.DropTable(
                name: "ValuationTestScopes");

            migrationBuilder.DropTable(
                name: "ValuationTestRuns");
        }
    }
}
