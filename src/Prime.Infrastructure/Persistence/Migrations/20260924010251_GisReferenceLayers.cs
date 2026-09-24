using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GisReferenceLayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BarangayBoundaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Geometry = table.Column<MultiPolygon>(type: "geometry(MultiPolygon,4326)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BarangayBoundaries", x => x.Id);
                    table.CheckConstraint("CK_BarangayBoundaries_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" > \"EffectiveDate\"");
                    table.ForeignKey(
                        name: "FK_BarangayBoundaries_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoadSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RoadTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Geometry = table.Column<MultiLineString>(type: "geometry(MultiLineString,4326)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoadSegments", x => x.Id);
                    table.CheckConstraint("CK_RoadSegments_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" > \"EffectiveDate\"");
                    table.ForeignKey(
                        name: "FK_RoadSegments_RoadTypes_RoadTypeId",
                        column: x => x.RoadTypeId,
                        principalTable: "RoadTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ZoneBoundaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Geometry = table.Column<MultiPolygon>(type: "geometry(MultiPolygon,4326)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneBoundaries", x => x.Id);
                    table.CheckConstraint("CK_ZoneBoundaries_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" > \"EffectiveDate\"");
                    table.ForeignKey(
                        name: "FK_ZoneBoundaries_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BarangayBoundaries_BarangayId_EffectiveDate",
                table: "BarangayBoundaries",
                columns: new[] { "BarangayId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BarangayBoundaries_Geometry",
                table: "BarangayBoundaries",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_BarangayBoundaries_ImportBatchId",
                table: "BarangayBoundaries",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "UX_BarangayBoundaries_Current",
                table: "BarangayBoundaries",
                column: "BarangayId",
                unique: true,
                filter: "\"EndDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RoadSegments_Code_EffectiveDate",
                table: "RoadSegments",
                columns: new[] { "Code", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RoadSegments_Geometry",
                table: "RoadSegments",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_RoadSegments_ImportBatchId",
                table: "RoadSegments",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_RoadSegments_RoadTypeId",
                table: "RoadSegments",
                column: "RoadTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_RoadSegments_Current",
                table: "RoadSegments",
                column: "Code",
                unique: true,
                filter: "\"EndDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneBoundaries_Geometry",
                table: "ZoneBoundaries",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneBoundaries_ImportBatchId",
                table: "ZoneBoundaries",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneBoundaries_ZoneId_EffectiveDate",
                table: "ZoneBoundaries",
                columns: new[] { "ZoneId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UX_ZoneBoundaries_Current",
                table: "ZoneBoundaries",
                column: "ZoneId",
                unique: true,
                filter: "\"EndDate\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BarangayBoundaries");

            migrationBuilder.DropTable(
                name: "RoadSegments");

            migrationBuilder.DropTable(
                name: "ZoneBoundaries");
        }
    }
}
