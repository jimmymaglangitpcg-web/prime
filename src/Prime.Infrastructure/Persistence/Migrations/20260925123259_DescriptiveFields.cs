using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DescriptiveFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BoundaryEast",
                table: "Property",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoundaryNorth",
                table: "Property",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoundarySouth",
                table: "Property",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoundaryWest",
                table: "Property",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TitleDate",
                table: "Property",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TitleTypeId",
                table: "Property",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "MachineryUnits",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearInstalled",
                table: "MachineryUnits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearOfInitialOperation",
                table: "MachineryUnits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BuildingPermitDate",
                table: "Buildings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildingPermitNumber",
                table: "Buildings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CertificateOfCompletionDate",
                table: "Buildings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CertificateOfOccupancyDate",
                table: "Buildings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CondominiumCertificateNumber",
                table: "Buildings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateConstructed",
                table: "Buildings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOccupied",
                table: "Buildings",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BuildingFloors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    FloorNumber = table.Column<int>(type: "integer", nullable: false),
                    Area = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingFloors", x => x.Id);
                    table.CheckConstraint("CK_BuildingFloors_Area", "\"Area\" > 0");
                    table.CheckConstraint("CK_BuildingFloors_FloorNumber", "\"FloorNumber\" >= 1");
                    table.ForeignKey(
                        name: "FK_BuildingFloors_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StructuralParts",
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
                    table.PrimaryKey("PK_StructuralParts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TitleTypes",
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
                    table.PrimaryKey("PK_TitleTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransferTaxClearances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CarNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CarDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TransferorName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TransferorTin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TransfereeTin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CapitalGainsTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CapitalGainsTaxReceipt = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CapitalGainsTaxDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DocumentaryStampTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DocumentaryStampTaxReceipt = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DocumentaryStampTaxDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TransferTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TransferTaxReceipt = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TransferTaxDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferTaxClearances", x => x.Id);
                    table.CheckConstraint("CK_TransferTaxClearances_Amounts", "COALESCE(\"CapitalGainsTax\", 0) >= 0 AND COALESCE(\"DocumentaryStampTax\", 0) >= 0 AND COALESCE(\"TransferTax\", 0) >= 0");
                    table.ForeignKey(
                        name: "FK_TransferTaxClearances_PropertyTransactions_PropertyTransact~",
                        column: x => x.PropertyTransactionId,
                        principalTable: "PropertyTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StructuralMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StructuralPartId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_StructuralMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StructuralMaterials_StructuralParts_StructuralPartId",
                        column: x => x.StructuralPartId,
                        principalTable: "StructuralParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BuildingMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    StructuralPartId = table.Column<Guid>(type: "uuid", nullable: false),
                    StructuralMaterialId = table.Column<Guid>(type: "uuid", nullable: true),
                    OtherSpecify = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FloorNumber = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingMaterials", x => x.Id);
                    table.CheckConstraint("CK_BuildingMaterials_FloorNumber", "\"FloorNumber\" IS NULL OR \"FloorNumber\" >= 1");
                    table.CheckConstraint("CK_BuildingMaterials_Material", "(\"StructuralMaterialId\" IS NULL) <> (\"OtherSpecify\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_BuildingMaterials_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BuildingMaterials_StructuralMaterials_StructuralMaterialId",
                        column: x => x.StructuralMaterialId,
                        principalTable: "StructuralMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BuildingMaterials_StructuralParts_StructuralPartId",
                        column: x => x.StructuralPartId,
                        principalTable: "StructuralParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Property_TitleTypeId",
                table: "Property",
                column: "TitleTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingFloors_BuildingId_FloorNumber",
                table: "BuildingFloors",
                columns: new[] { "BuildingId", "FloorNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuildingMaterials_BuildingId",
                table: "BuildingMaterials",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingMaterials_StructuralMaterialId",
                table: "BuildingMaterials",
                column: "StructuralMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingMaterials_StructuralPartId",
                table: "BuildingMaterials",
                column: "StructuralPartId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuralMaterials_Code",
                table: "StructuralMaterials",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StructuralMaterials_StructuralPartId",
                table: "StructuralMaterials",
                column: "StructuralPartId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuralParts_Code",
                table: "StructuralParts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TitleTypes_Code",
                table: "TitleTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferTaxClearances_PropertyTransactionId",
                table: "TransferTaxClearances",
                column: "PropertyTransactionId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Property_TitleTypes_TitleTypeId",
                table: "Property",
                column: "TitleTypeId",
                principalTable: "TitleTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            // Starting reference lists from the MRPAAO (docs/analysis/mrpaao-forms-model.md §10.4):
            // descriptors, not values; editable, and skipped when a code already exists.
            migrationBuilder.Sql("""
                INSERT INTO "TitleTypes" ("Id", "Code", "Name", "Description", "SortOrder", "IsActive", "CreatedAt") VALUES
                    (gen_random_uuid(), 'OCT', 'Original Certificate of Title', 'MRPAAO 2004 (superseded) Att. 1, 4 - starting list, editable', 1, TRUE, now()),
                    (gen_random_uuid(), 'TCT', 'Transfer Certificate of Title', 'MRPAAO 2004 (superseded) Att. 1, 4 - starting list, editable', 2, TRUE, now()),
                    (gen_random_uuid(), 'CLOA', 'Certificate of Land Ownership Award', 'MRPAAO 2004 (superseded) Att. 1, 4 - starting list, editable', 3, TRUE, now()),
                    (gen_random_uuid(), 'CCT', 'Condominium Certificate of Title', 'MRPAAO 2004 (superseded) Att. 1, 4 - starting list, editable', 4, TRUE, now())
                ON CONFLICT ("Code") DO NOTHING;
                INSERT INTO "StructuralParts" ("Id", "Code", "Name", "Description", "SortOrder", "IsActive", "CreatedAt") VALUES
                    (gen_random_uuid(), 'FOUNDATION', 'Foundation', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now()),
                    (gen_random_uuid(), 'COLUMNS', 'Columns', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now()),
                    (gen_random_uuid(), 'BEAMS', 'Beams', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 3, TRUE, now()),
                    (gen_random_uuid(), 'ROOF_FRAMING', 'Roof Framing', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 4, TRUE, now()),
                    (gen_random_uuid(), 'ROOFING', 'Roofing', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 5, TRUE, now()),
                    (gen_random_uuid(), 'EXTERIOR_WALLS', 'Exterior Walls', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 6, TRUE, now()),
                    (gen_random_uuid(), 'FLOORING', 'Flooring', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 7, TRUE, now()),
                    (gen_random_uuid(), 'DOORS', 'Doors', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 8, TRUE, now()),
                    (gen_random_uuid(), 'CEILING', 'Ceiling', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 9, TRUE, now()),
                    (gen_random_uuid(), 'WINDOWS', 'Windows', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 10, TRUE, now()),
                    (gen_random_uuid(), 'STAIRS', 'Stairs', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 11, TRUE, now()),
                    (gen_random_uuid(), 'PARTITION', 'Partition', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 12, TRUE, now()),
                    (gen_random_uuid(), 'WALL_FINISH', 'Wall Finish', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 13, TRUE, now()),
                    (gen_random_uuid(), 'ELECTRICAL_CONDUIT', 'Electrical Conduit', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 14, TRUE, now()),
                    (gen_random_uuid(), 'TOILET_BATH', 'Toilet and Bath', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 15, TRUE, now()),
                    (gen_random_uuid(), 'PLUMBING_SEWERS', 'Plumbing and Sewers', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 16, TRUE, now())
                ON CONFLICT ("Code") DO NOTHING;
                INSERT INTO "StructuralMaterials" ("Id", "StructuralPartId", "Code", "Name", "Description", "SortOrder", "IsActive", "CreatedAt")
                SELECT gen_random_uuid(), p."Id", 'FOUNDATION-REINFORCED_CONCRETE', 'Reinforced Concrete', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'FOUNDATION'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'FOUNDATION-PLAIN_CONCRETE', 'Plain Concrete', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'FOUNDATION'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'COLUMNS-STEEL', 'Steel', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'COLUMNS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'COLUMNS-REINFORCED_CONCRETE', 'Reinforced Concrete', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'COLUMNS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'COLUMNS-WOOD', 'Wood', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 3, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'COLUMNS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'BEAMS-REINFORCED_CONCRETE', 'Reinforced Concrete', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'BEAMS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'BEAMS-WOOD', 'Wood', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'BEAMS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'BEAMS-STEEL', 'Steel', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 3, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'BEAMS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'ROOF_FRAMING-STEEL', 'Steel', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'ROOF_FRAMING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'ROOF_FRAMING-WOOD', 'Wood', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'ROOF_FRAMING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'ROOFING-G_I_SHEET', 'G.I. Sheet', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'ROOFING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'ROOFING-ASBESTOS', 'Asbestos', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'ROOFING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'ROOFING-CERAMIC_TILES', 'Ceramic Tiles', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 3, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'ROOFING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'ROOFING-CONCRETE_DECK', 'Concrete Deck', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 4, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'ROOFING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'ROOFING-ALUMINUM', 'Aluminum', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 5, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'ROOFING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'ROOFING-LONG_SPAN', 'Long Span', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 6, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'ROOFING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'ROOFING-NIPA_ANAHAW_COGON', 'Nipa/Anahaw/Cogon', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 7, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'ROOFING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'EXTERIOR_WALLS-PLAIN_CONCRETE', 'Plain Concrete', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'EXTERIOR_WALLS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'EXTERIOR_WALLS-WOOD', 'Wood', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'EXTERIOR_WALLS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'EXTERIOR_WALLS-G_I_SHEETS', 'G.I. Sheets', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 3, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'EXTERIOR_WALLS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'EXTERIOR_WALLS-CHB', 'CHB', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 4, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'EXTERIOR_WALLS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'EXTERIOR_WALLS-REINFORCED_CONCRETE', 'Reinforced Concrete', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 5, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'EXTERIOR_WALLS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'EXTERIOR_WALLS-BUILD_A_WALL', 'Build-a-wall', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 6, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'EXTERIOR_WALLS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'EXTERIOR_WALLS-BAMBOO', 'Bamboo', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 7, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'EXTERIOR_WALLS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'EXTERIOR_WALLS-SAWALI', 'Sawali', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 8, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'EXTERIOR_WALLS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'FLOORING-PLAIN_CEMENT', 'Plain Cement', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'FLOORING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'FLOORING-WOOD', 'Wood', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'FLOORING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'FLOORING-MARBLE', 'Marble', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 3, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'FLOORING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'FLOORING-TILES', 'Tiles', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 4, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'FLOORING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'FLOORING-REINFORCED_CONCRETE_UPPER_FLOORS', 'Reinforced Concrete (upper floors)', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 5, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'FLOORING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'DOORS-STEEL', 'Steel', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'DOORS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'DOORS-ALUMINUM', 'Aluminum', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'DOORS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'DOORS-WOOD', 'Wood', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 3, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'DOORS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'DOORS-GLASS', 'Glass', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 4, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'DOORS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'DOORS-GRILLS', 'Grills', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 5, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'DOORS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'DOORS-PLASTIC_INDOOR_USE', 'Plastic (indoor use)', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 6, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'DOORS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'CEILING-WOOD', 'Wood', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'CEILING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'CEILING-LAWANIT', 'Lawanit', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'CEILING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'CEILING-ASBESTOS', 'Asbestos', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 3, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'CEILING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'CEILING-DURAFLEX', 'Duraflex', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 4, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'CEILING'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'WINDOWS-STEEL', 'Steel', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'WINDOWS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'WINDOWS-ALUMINUM', 'Aluminum', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'WINDOWS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'WINDOWS-WOOD', 'Wood', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 3, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'WINDOWS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'WINDOWS-GLASS', 'Glass', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 4, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'WINDOWS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'WINDOWS-GRILLS', 'Grills', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 5, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'WINDOWS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'STAIRS-STEEL', 'Steel', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'STAIRS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'STAIRS-REINFORCED_CONCRETE', 'Reinforced Concrete', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'STAIRS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'STAIRS-WOOD', 'Wood', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 3, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'STAIRS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'PARTITION-REINFORCED_CONCRETE', 'Reinforced Concrete', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'PARTITION'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'PARTITION-CONCRETE_HOLLOW_BLOCKS', 'Concrete Hollow Blocks', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'PARTITION'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'PARTITION-GI_SHEETS', 'GI Sheets', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 3, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'PARTITION'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'PARTITION-WOOD', 'Wood', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 4, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'PARTITION'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'WALL_FINISH-CEMENT_PLASTER', 'Cement Plaster', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'WALL_FINISH'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'WALL_FINISH-WOODEN_PANELS', 'Wooden Panels', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'WALL_FINISH'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'ELECTRICAL_CONDUIT-PVC_CONDUIT', 'PVC Conduit', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'ELECTRICAL_CONDUIT'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'ELECTRICAL_CONDUIT-METAL_MOLDING', 'Metal Molding', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'ELECTRICAL_CONDUIT'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'ELECTRICAL_CONDUIT-WOOD_MOLDING', 'Wood Molding', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 3, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'ELECTRICAL_CONDUIT'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'ELECTRICAL_CONDUIT-OPEN_WIRING', 'Open Wiring', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 4, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'ELECTRICAL_CONDUIT'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'ELECTRICAL_CONDUIT-BX', 'BX', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 5, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'ELECTRICAL_CONDUIT'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'TOILET_BATH-TILES', 'Tiles', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'TOILET_BATH'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'TOILET_BATH-MARBLE', 'Marble', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'TOILET_BATH'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'TOILET_BATH-PLAIN_CEMENT', 'Plain Cement', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 3, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'TOILET_BATH'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'PLUMBING_SEWERS-STEEL_PIPES', 'Steel Pipes', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 1, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'PLUMBING_SEWERS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'PLUMBING_SEWERS-PVC', 'PVC', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 2, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'PLUMBING_SEWERS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'PLUMBING_SEWERS-CONCRETE_PIPES', 'Concrete Pipes', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 3, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'PLUMBING_SEWERS'
                UNION ALL SELECT gen_random_uuid(), p."Id", 'PLUMBING_SEWERS-ASBESTOS_PIPES', 'Asbestos Pipes', 'MRPAAO 2004 (superseded) p.150-152 - starting list, editable', 4, TRUE, now() FROM "StructuralParts" p WHERE p."Code" = 'PLUMBING_SEWERS'
                ON CONFLICT ("Code") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Property_TitleTypes_TitleTypeId",
                table: "Property");

            migrationBuilder.DropTable(
                name: "BuildingFloors");

            migrationBuilder.DropTable(
                name: "BuildingMaterials");

            migrationBuilder.DropTable(
                name: "TitleTypes");

            migrationBuilder.DropTable(
                name: "TransferTaxClearances");

            migrationBuilder.DropTable(
                name: "StructuralMaterials");

            migrationBuilder.DropTable(
                name: "StructuralParts");

            migrationBuilder.DropIndex(
                name: "IX_Property_TitleTypeId",
                table: "Property");

            migrationBuilder.DropColumn(
                name: "BoundaryEast",
                table: "Property");

            migrationBuilder.DropColumn(
                name: "BoundaryNorth",
                table: "Property");

            migrationBuilder.DropColumn(
                name: "BoundarySouth",
                table: "Property");

            migrationBuilder.DropColumn(
                name: "BoundaryWest",
                table: "Property");

            migrationBuilder.DropColumn(
                name: "TitleDate",
                table: "Property");

            migrationBuilder.DropColumn(
                name: "TitleTypeId",
                table: "Property");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "YearInstalled",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "YearOfInitialOperation",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "BuildingPermitDate",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "BuildingPermitNumber",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "CertificateOfCompletionDate",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "CertificateOfOccupancyDate",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "CondominiumCertificateNumber",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "DateConstructed",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "DateOccupied",
                table: "Buildings");
        }
    }
}
