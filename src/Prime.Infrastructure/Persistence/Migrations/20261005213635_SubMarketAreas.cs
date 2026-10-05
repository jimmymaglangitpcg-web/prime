using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubMarketAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubMarketAreas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_SubMarketAreas", x => x.Id);
                    table.CheckConstraint("CK_SubMarketAreas_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" > \"EffectiveDate\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubMarketAreas_Code_EffectiveDate",
                table: "SubMarketAreas",
                columns: new[] { "Code", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SubMarketAreas_Geometry",
                table: "SubMarketAreas",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_SubMarketAreas_ImportBatchId",
                table: "SubMarketAreas",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "UX_SubMarketAreas_Current",
                table: "SubMarketAreas",
                column: "Code",
                unique: true,
                filter: "\"EndDate\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubMarketAreas");
        }
    }
}
