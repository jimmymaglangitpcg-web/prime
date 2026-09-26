using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SwornStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SwornStatements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeclarantName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DeclarantTaxpayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Citizenship = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CivilStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PostalAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DeclarantTin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Capacity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OwnerNames = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilingBasis = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SignedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    SignedAt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Thumbmarked = table.Column<bool>(type: "boolean", nullable: false),
                    Witness1 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Witness2 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SwornOn = table.Column<DateOnly>(type: "date", nullable: true),
                    SwornAt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AdministeringOfficer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    OfficerTin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IdentityDocument = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IdentityDocumentIssuedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IdentityDocumentIssuedAt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FiledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FiledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SupersedesId = table.Column<Guid>(type: "uuid", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwornStatements", x => x.Id);
                    table.CheckConstraint("CK_SwornStatements_Cancelled", "(\"Status\" = 'Cancelled') = (\"CancelledAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_SwornStatements_Filed", "(\"Status\" IN ('Filed', 'Superseded')) <= (\"FiledAt\" IS NOT NULL AND \"SignedOn\" IS NOT NULL AND \"SwornOn\" IS NOT NULL AND \"AdministeringOfficer\" IS NOT NULL AND \"ReceivedOn\" IS NOT NULL)");
                    table.CheckConstraint("CK_SwornStatements_NotSelf", "\"SupersedesId\" IS NULL OR \"SupersedesId\" <> \"Id\"");
                    table.CheckConstraint("CK_SwornStatements_Owners", "\"Capacity\" = 'Owner' OR \"OwnerNames\" IS NOT NULL");
                    table.CheckConstraint("CK_SwornStatements_Witnesses", "NOT \"Thumbmarked\" OR \"Status\" = 'Draft' OR \"Status\" = 'Cancelled' OR (\"Witness1\" IS NOT NULL AND \"Witness2\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SwornStatements_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwornStatements_SwornStatements_SupersedesId",
                        column: x => x.SupersedesId,
                        principalTable: "SwornStatements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwornStatements_Taxpayers_DeclarantTaxpayerId",
                        column: x => x.DeclarantTaxpayerId,
                        principalTable: "Taxpayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SwornStatementItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SwornStatementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    TaxDeclarationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExistingTdNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: true),
                    RpuId = table.Column<Guid>(type: "uuid", nullable: true),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeclaredMarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LotNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BlockNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CadastralNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TitleNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Area = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    AreaUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    FloorArea = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Storeys = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    YearCompleted = table.Column<int>(type: "integer", nullable: true),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LotOwnerName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DateAcquired = table.Column<DateOnly>(type: "date", nullable: true),
                    DateOperationCommenced = table.Column<DateOnly>(type: "date", nullable: true),
                    AcquisitionCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    InstallationCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Depreciation = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ImprovementKindId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductiveCount = table.Column<int>(type: "integer", nullable: true),
                    NonProductiveCount = table.Column<int>(type: "integer", nullable: true),
                    AnnualProduct = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Ages = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwornStatementItems", x => x.Id);
                    table.CheckConstraint("CK_SwornStatementItems_Amounts", "COALESCE(\"AcquisitionCost\", 0) >= 0 AND COALESCE(\"InstallationCost\", 0) >= 0 AND COALESCE(\"Depreciation\", 0) >= 0 AND COALESCE(\"ProductiveCount\", 0) >= 0 AND COALESCE(\"NonProductiveCount\", 0) >= 0 AND COALESCE(\"Storeys\", 1) >= 1");
                    table.CheckConstraint("CK_SwornStatementItems_ExistingTd", "\"TaxDeclarationId\" IS NULL OR \"ExistingTdNumber\" IS NULL");
                    table.CheckConstraint("CK_SwornStatementItems_Kind", "(\"Kind\" = 'Land' AND \"Area\" > 0 AND \"AreaUnit\" IS NOT NULL) OR (\"Kind\" = 'Building' AND \"FloorArea\" > 0) OR (\"Kind\" = 'Machinery' AND \"Description\" IS NOT NULL) OR (\"Kind\" = 'OtherImprovement' AND \"ImprovementKindId\" IS NOT NULL)");
                    table.CheckConstraint("CK_SwornStatementItems_Sequence", "\"Sequence\" >= 1");
                    table.CheckConstraint("CK_SwornStatementItems_Unit", "\"RpuId\" IS NULL OR \"PropertyId\" IS NOT NULL");
                    table.CheckConstraint("CK_SwornStatementItems_Value", "\"DeclaredMarketValue\" >= 0");
                    table.ForeignKey(
                        name: "FK_SwornStatementItems_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwornStatementItems_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwornStatementItems_ImprovementKinds_ImprovementKindId",
                        column: x => x.ImprovementKindId,
                        principalTable: "ImprovementKinds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwornStatementItems_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwornStatementItems_RealPropertyUnit_RpuId",
                        column: x => x.RpuId,
                        principalTable: "RealPropertyUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwornStatementItems_SwornStatements_SwornStatementId",
                        column: x => x.SwornStatementId,
                        principalTable: "SwornStatements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwornStatementItems_TaxDeclarations_TaxDeclarationId",
                        column: x => x.TaxDeclarationId,
                        principalTable: "TaxDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SwornStatementItems_ActualUseId",
                table: "SwornStatementItems",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_SwornStatementItems_ClassificationId",
                table: "SwornStatementItems",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_SwornStatementItems_ExistingTdNumber",
                table: "SwornStatementItems",
                column: "ExistingTdNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SwornStatementItems_ImprovementKindId",
                table: "SwornStatementItems",
                column: "ImprovementKindId");

            migrationBuilder.CreateIndex(
                name: "IX_SwornStatementItems_PropertyId",
                table: "SwornStatementItems",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_SwornStatementItems_RpuId",
                table: "SwornStatementItems",
                column: "RpuId");

            migrationBuilder.CreateIndex(
                name: "IX_SwornStatementItems_SwornStatementId_Sequence",
                table: "SwornStatementItems",
                columns: new[] { "SwornStatementId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwornStatementItems_TaxDeclarationId",
                table: "SwornStatementItems",
                column: "TaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_SwornStatements_DeclarantName",
                table: "SwornStatements",
                column: "DeclarantName");

            migrationBuilder.CreateIndex(
                name: "IX_SwornStatements_DeclarantTaxpayerId",
                table: "SwornStatements",
                column: "DeclarantTaxpayerId");

            migrationBuilder.CreateIndex(
                name: "IX_SwornStatements_MunicipalityId_Status",
                table: "SwornStatements",
                columns: new[] { "MunicipalityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SwornStatements_Number",
                table: "SwornStatements",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwornStatements_ReceivedOn",
                table: "SwornStatements",
                column: "ReceivedOn");

            migrationBuilder.CreateIndex(
                name: "UX_SwornStatements_Supersedes_Live",
                table: "SwornStatements",
                column: "SupersedesId",
                unique: true,
                filter: "\"SupersedesId\" IS NOT NULL AND \"Status\" <> 'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SwornStatementItems");

            migrationBuilder.DropTable(
                name: "SwornStatements");
        }
    }
}
