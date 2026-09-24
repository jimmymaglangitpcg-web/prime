using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TypedParcelGeometry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-written instead of the scaffolded AlterColumn: USING
            // ST_Multi wraps any existing single POLYGON losslessly. Rows
            // with a different SRID or non-areal geometry make this fail
            // loudly rather than being silently reprojected or dropped
            // (CLAUDE.md §105) — resolve those by hand before re-running.
            migrationBuilder.Sql(
                """ALTER TABLE "Parcels" ALTER COLUMN "Geometry" TYPE geometry(MultiPolygon,4326) USING ST_Multi("Geometry");""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Geometry>(
                name: "Geometry",
                table: "Parcels",
                type: "geometry",
                nullable: true,
                oldClrType: typeof(Geometry),
                oldType: "geometry(MultiPolygon,4326)",
                oldNullable: true);
        }
    }
}
