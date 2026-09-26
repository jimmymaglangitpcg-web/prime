using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PinAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParcelNumber",
                table: "Parcels",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SectionId",
                table: "Parcels",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PinAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ParcelId = table.Column<Guid>(type: "uuid", nullable: true),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: true),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParcelNumber = table.Column<int>(type: "integer", nullable: true),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetirementReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PropertyTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PinAssignments", x => x.Id);
                    table.CheckConstraint("CK_PinAssignments_Permanent", "\"Kind\" <> 'Permanent' OR (\"ParcelId\" IS NOT NULL AND \"SectionId\" IS NOT NULL AND \"ParcelNumber\" IS NOT NULL)");
                    table.CheckConstraint("CK_PinAssignments_Retired", "(\"RetiredAt\" IS NULL) = (\"RetirementReason\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_PinAssignments_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PinAssignments_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PinAssignments_PropertyTransactions_PropertyTransactionId",
                        column: x => x.PropertyTransactionId,
                        principalTable: "PropertyTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PinAssignments_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PinAssignments_TaxMapSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "TaxMapSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_SectionId_ParcelNumber",
                table: "Parcels",
                columns: new[] { "SectionId", "ParcelNumber" },
                unique: true,
                filter: "\"SectionId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Parcels_SectionNumber",
                table: "Parcels",
                sql: "(\"SectionId\" IS NULL) = (\"ParcelNumber\" IS NULL) AND (\"ParcelNumber\" IS NULL OR \"ParcelNumber\" > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_PinAssignments_BarangayId",
                table: "PinAssignments",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_PinAssignments_ParcelId",
                table: "PinAssignments",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_PinAssignments_Pin",
                table: "PinAssignments",
                column: "Pin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PinAssignments_PropertyTransactionId",
                table: "PinAssignments",
                column: "PropertyTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PinAssignments_SectionId",
                table: "PinAssignments",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "UX_PinAssignments_Current",
                table: "PinAssignments",
                column: "PropertyId",
                unique: true,
                filter: "\"RetiredAt\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Parcels_TaxMapSections_SectionId",
                table: "Parcels",
                column: "SectionId",
                principalTable: "TaxMapSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Every existing property's PIN becomes its current, "Registered" assignment
            // (docs/analysis/property-identification.md §3.4), so PIN history is complete
            // from the start and no PIN given before can be given again.
            migrationBuilder.Sql("""
                INSERT INTO "PinAssignments" ("Id", "PropertyId", "Pin", "Kind", "AssignedAt", "CreatedAt")
                SELECT gen_random_uuid(), p."Id", p."PropertyIdentificationNumber", 'Registered', p."CreatedAt", now()
                FROM "Property" p
                WHERE NOT EXISTS (SELECT 1 FROM "PinAssignments" a WHERE a."PropertyId" = p."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Parcels_TaxMapSections_SectionId",
                table: "Parcels");

            migrationBuilder.DropTable(
                name: "PinAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_SectionId_ParcelNumber",
                table: "Parcels");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Parcels_SectionNumber",
                table: "Parcels");

            migrationBuilder.DropColumn(
                name: "ParcelNumber",
                table: "Parcels");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "Parcels");
        }
    }
}
