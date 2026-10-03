using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MarketData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TransferTaxClearances_Amounts",
                table: "TransferTaxClearances");

            migrationBuilder.AddColumn<decimal>(
                name: "Consideration",
                table: "TransferTaxClearances",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BuildingPermitAbstracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PermitNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ProposedConstructionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpectedCompletionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PermitteeName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PermitteeAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TaxDeclarationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: true),
                    BlockLotNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Street = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Scope = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BuildingTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    StructuralTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Storeys = table.Column<int>(type: "integer", nullable: true),
                    TotalFloorArea = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_BuildingPermitAbstracts", x => x.Id);
                    table.CheckConstraint("CK_BuildingPermitAbstracts_Amounts", "COALESCE(\"Storeys\", 1) >= 1 AND COALESCE(\"TotalFloorArea\", 0) >= 0 AND COALESCE(\"EstimatedCost\", 0) >= 0");
                    table.CheckConstraint("CK_BuildingPermitAbstracts_Cancelled", "(\"CancelledAt\" IS NULL) = (\"CancellationReason\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_BuildingPermitAbstracts_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BuildingPermitAbstracts_BuildingTypes_BuildingTypeId",
                        column: x => x.BuildingTypeId,
                        principalTable: "BuildingTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BuildingPermitAbstracts_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BuildingPermitAbstracts_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BuildingPermitAbstracts_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BuildingPermitAbstracts_StructuralTypes_StructuralTypeId",
                        column: x => x.StructuralTypeId,
                        principalTable: "StructuralTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConveyanceModes",
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
                    table.PrimaryKey("PK_ConveyanceModes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MachineryRegistrationAbstracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    OwnerName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OwnerAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TaxDeclarationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: true),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MachineryTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BrandModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    YearAcquired = table.Column<int>(type: "integer", nullable: true),
                    Manufacturer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrentCondition = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InstallationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    MachineryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_MachineryRegistrationAbstracts", x => x.Id);
                    table.CheckConstraint("CK_MachineryRegistrationAbstracts_Cancelled", "(\"CancelledAt\" IS NULL) = (\"CancellationReason\" IS NULL)");
                    table.CheckConstraint("CK_MachineryRegistrationAbstracts_Cost", "COALESCE(\"Cost\", 0) >= 0");
                    table.ForeignKey(
                        name: "FK_MachineryRegistrationAbstracts_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineryRegistrationAbstracts_MachineryTypes_MachineryType~",
                        column: x => x.MachineryTypeId,
                        principalTable: "MachineryTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineryRegistrationAbstracts_MachineryUnits_MachineryId",
                        column: x => x.MachineryId,
                        principalTable: "MachineryUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineryRegistrationAbstracts_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketDataReportRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDataReportRuns", x => x.Id);
                    table.CheckConstraint("CK_MarketDataReportRuns_Period", "\"FromDate\" <= \"ToDate\"");
                    table.ForeignKey(
                        name: "FK_MarketDataReportRuns_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ConveyanceModeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DocumentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DocumentFileNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GrantorNames = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GranteeNames = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GranteeAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: true),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Pin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TaxDeclarationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PreviousTitleNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NewTitleNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConveysLand = table.Column<bool>(type: "boolean", nullable: false),
                    ConveysBuilding = table.Column<bool>(type: "boolean", nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: true),
                    BuildingTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    StructuralTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    LandArea = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    LandAreaUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BuildingFloorArea = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Consideration = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LandConsideration = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LandUnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BuildingUnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Review = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExclusionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FieldValidatedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PropertyTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImportBatch = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_MarketTransactions", x => x.Id);
                    table.CheckConstraint("CK_MarketTransactions_Amounts", "\"Consideration\" >= 0 AND COALESCE(\"LandConsideration\", 0) >= 0 AND COALESCE(\"LandConsideration\", 0) <= \"Consideration\" AND COALESCE(\"LandArea\", 0) >= 0 AND COALESCE(\"BuildingFloorArea\", 0) >= 0");
                    table.CheckConstraint("CK_MarketTransactions_Cancelled", "(\"CancelledAt\" IS NULL) = (\"CancellationReason\" IS NULL)");
                    table.CheckConstraint("CK_MarketTransactions_Conveys", "\"ConveysLand\" OR \"ConveysBuilding\"");
                    table.CheckConstraint("CK_MarketTransactions_Excluded", "(\"Review\" = 'Excluded') = (\"ExclusionReason\" IS NOT NULL)");
                    table.CheckConstraint("CK_MarketTransactions_Reviewed", "(\"Review\" = 'Unreviewed') = (\"ReviewedAt\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_MarketTransactions_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTransactions_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTransactions_BuildingTypes_BuildingTypeId",
                        column: x => x.BuildingTypeId,
                        principalTable: "BuildingTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTransactions_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTransactions_ConveyanceModes_ConveyanceModeId",
                        column: x => x.ConveyanceModeId,
                        principalTable: "ConveyanceModes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTransactions_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTransactions_PropertyTransactions_PropertyTransaction~",
                        column: x => x.PropertyTransactionId,
                        principalTable: "PropertyTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTransactions_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTransactions_StructuralTypes_StructuralTypeId",
                        column: x => x.StructuralTypeId,
                        principalTable: "StructuralTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTransactions_SubClassifications_SubClassificationId",
                        column: x => x.SubClassificationId,
                        principalTable: "SubClassifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransferTaxClearances_Amounts",
                table: "TransferTaxClearances",
                sql: "COALESCE(\"CapitalGainsTax\", 0) >= 0 AND COALESCE(\"DocumentaryStampTax\", 0) >= 0 AND COALESCE(\"TransferTax\", 0) >= 0 AND COALESCE(\"Consideration\", 0) >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingPermitAbstracts_BarangayId",
                table: "BuildingPermitAbstracts",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingPermitAbstracts_BuildingId",
                table: "BuildingPermitAbstracts",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingPermitAbstracts_BuildingTypeId",
                table: "BuildingPermitAbstracts",
                column: "BuildingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingPermitAbstracts_ClassificationId",
                table: "BuildingPermitAbstracts",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingPermitAbstracts_MunicipalityId_PermitNumber",
                table: "BuildingPermitAbstracts",
                columns: new[] { "MunicipalityId", "PermitNumber" },
                unique: true,
                filter: "\"CancelledAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingPermitAbstracts_StructuralTypeId",
                table: "BuildingPermitAbstracts",
                column: "StructuralTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ConveyanceModes_Code",
                table: "ConveyanceModes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRegistrationAbstracts_BarangayId",
                table: "MachineryRegistrationAbstracts",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRegistrationAbstracts_MachineryId",
                table: "MachineryRegistrationAbstracts",
                column: "MachineryId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRegistrationAbstracts_MachineryTypeId",
                table: "MachineryRegistrationAbstracts",
                column: "MachineryTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRegistrationAbstracts_MunicipalityId_CertificateNu~",
                table: "MachineryRegistrationAbstracts",
                columns: new[] { "MunicipalityId", "CertificateNumber" },
                unique: true,
                filter: "\"CancelledAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataReportRuns_MunicipalityId",
                table: "MarketDataReportRuns",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_ActualUseId",
                table: "MarketTransactions",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_BarangayId",
                table: "MarketTransactions",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_BuildingTypeId",
                table: "MarketTransactions",
                column: "BuildingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_ClassificationId_SubClassificationId",
                table: "MarketTransactions",
                columns: new[] { "ClassificationId", "SubClassificationId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_ConveyanceModeId",
                table: "MarketTransactions",
                column: "ConveyanceModeId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_MunicipalityId_TransactionDate",
                table: "MarketTransactions",
                columns: new[] { "MunicipalityId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_PropertyId",
                table: "MarketTransactions",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_PropertyTransactionId",
                table: "MarketTransactions",
                column: "PropertyTransactionId",
                unique: true,
                filter: "\"PropertyTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_StructuralTypeId",
                table: "MarketTransactions",
                column: "StructuralTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTransactions_SubClassificationId",
                table: "MarketTransactions",
                column: "SubClassificationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuildingPermitAbstracts");

            migrationBuilder.DropTable(
                name: "MachineryRegistrationAbstracts");

            migrationBuilder.DropTable(
                name: "MarketDataReportRuns");

            migrationBuilder.DropTable(
                name: "MarketTransactions");

            migrationBuilder.DropTable(
                name: "ConveyanceModes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TransferTaxClearances_Amounts",
                table: "TransferTaxClearances");

            migrationBuilder.DropColumn(
                name: "Consideration",
                table: "TransferTaxClearances");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransferTaxClearances_Amounts",
                table: "TransferTaxClearances",
                sql: "COALESCE(\"CapitalGainsTax\", 0) >= 0 AND COALESCE(\"DocumentaryStampTax\", 0) >= 0 AND COALESCE(\"TransferTax\", 0) >= 0");
        }
    }
}
