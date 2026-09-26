using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PropertyIdentificationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PinIndexNumber",
                table: "Provinces",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinIndexNumber",
                table: "Municipalities",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CityDistrictId",
                table: "Barangays",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinIndexNumber",
                table: "Barangays",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RetiredOn",
                table: "Barangays",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetirementReason",
                table: "Barangays",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SplitFromBarangayId",
                table: "Barangays",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CityDistricts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    IndexNumber = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CityDistricts", x => x.Id);
                    table.CheckConstraint("CK_CityDistricts_IndexNumber", "\"IndexNumber\" ~ '^[0-9]{2}$'");
                    table.ForeignKey(
                        name: "FK_CityDistricts_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxMapSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: false),
                    IndexNumber = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SplitFromSectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RetiredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    RetirementReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxMapSections", x => x.Id);
                    table.CheckConstraint("CK_TaxMapSections_IndexNumber", "\"IndexNumber\" ~ '^[0-9]{3}$'");
                    table.CheckConstraint("CK_TaxMapSections_Retired", "(\"RetiredOn\" IS NULL) = (\"RetirementReason\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_TaxMapSections_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxMapSections_TaxMapSections_SplitFromSectionId",
                        column: x => x.SplitFromSectionId,
                        principalTable: "TaxMapSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Provinces_PinIndexNumber",
                table: "Provinces",
                column: "PinIndexNumber",
                unique: true,
                filter: "\"PinIndexNumber\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Province_PinIndexNumber",
                table: "Provinces",
                sql: "\"PinIndexNumber\" ~ '^[0-9]{3}$'");

            migrationBuilder.CreateIndex(
                name: "IX_Municipalities_ProvinceId_PinIndexNumber",
                table: "Municipalities",
                columns: new[] { "ProvinceId", "PinIndexNumber" },
                unique: true,
                filter: "\"PinIndexNumber\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Municipality_PinIndexNumber",
                table: "Municipalities",
                sql: "\"PinIndexNumber\" ~ '^[0-9]{2,3}$'");

            migrationBuilder.CreateIndex(
                name: "IX_Barangays_CityDistrictId",
                table: "Barangays",
                column: "CityDistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Barangays_SplitFromBarangayId",
                table: "Barangays",
                column: "SplitFromBarangayId");

            migrationBuilder.CreateIndex(
                name: "UX_Barangay_PinIndexNumber",
                table: "Barangays",
                columns: new[] { "MunicipalityId", "CityDistrictId", "PinIndexNumber" },
                unique: true,
                filter: "\"PinIndexNumber\" IS NOT NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Barangay_PinIndexNumber",
                table: "Barangays",
                sql: "\"PinIndexNumber\" ~ '^[0-9]{4}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Barangay_Retired",
                table: "Barangays",
                sql: "(\"RetiredOn\" IS NULL) = (\"RetirementReason\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_CityDistricts_MunicipalityId_IndexNumber",
                table: "CityDistricts",
                columns: new[] { "MunicipalityId", "IndexNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxMapSections_BarangayId_IndexNumber",
                table: "TaxMapSections",
                columns: new[] { "BarangayId", "IndexNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxMapSections_SplitFromSectionId",
                table: "TaxMapSections",
                column: "SplitFromSectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Barangays_Barangays_SplitFromBarangayId",
                table: "Barangays",
                column: "SplitFromBarangayId",
                principalTable: "Barangays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Barangays_CityDistricts_CityDistrictId",
                table: "Barangays",
                column: "CityDistrictId",
                principalTable: "CityDistricts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Barangays_Barangays_SplitFromBarangayId",
                table: "Barangays");

            migrationBuilder.DropForeignKey(
                name: "FK_Barangays_CityDistricts_CityDistrictId",
                table: "Barangays");

            migrationBuilder.DropTable(
                name: "CityDistricts");

            migrationBuilder.DropTable(
                name: "TaxMapSections");

            migrationBuilder.DropIndex(
                name: "IX_Provinces_PinIndexNumber",
                table: "Provinces");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Province_PinIndexNumber",
                table: "Provinces");

            migrationBuilder.DropIndex(
                name: "IX_Municipalities_ProvinceId_PinIndexNumber",
                table: "Municipalities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Municipality_PinIndexNumber",
                table: "Municipalities");

            migrationBuilder.DropIndex(
                name: "IX_Barangays_CityDistrictId",
                table: "Barangays");

            migrationBuilder.DropIndex(
                name: "IX_Barangays_SplitFromBarangayId",
                table: "Barangays");

            migrationBuilder.DropIndex(
                name: "UX_Barangay_PinIndexNumber",
                table: "Barangays");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Barangay_PinIndexNumber",
                table: "Barangays");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Barangay_Retired",
                table: "Barangays");

            migrationBuilder.DropColumn(
                name: "PinIndexNumber",
                table: "Provinces");

            migrationBuilder.DropColumn(
                name: "PinIndexNumber",
                table: "Municipalities");

            migrationBuilder.DropColumn(
                name: "CityDistrictId",
                table: "Barangays");

            migrationBuilder.DropColumn(
                name: "PinIndexNumber",
                table: "Barangays");

            migrationBuilder.DropColumn(
                name: "RetiredOn",
                table: "Barangays");

            migrationBuilder.DropColumn(
                name: "RetirementReason",
                table: "Barangays");

            migrationBuilder.DropColumn(
                name: "SplitFromBarangayId",
                table: "Barangays");
        }
    }
}
