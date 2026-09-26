using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaxMapSectionsLayerAndRolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SectionId",
                table: "RegisterRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SectionBoundaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_SectionBoundaries", x => x.Id);
                    table.CheckConstraint("CK_SectionBoundaries_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" > \"EffectiveDate\"");
                    table.ForeignKey(
                        name: "FK_SectionBoundaries_TaxMapSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "TaxMapSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegisterRuns_SectionId",
                table: "RegisterRuns",
                column: "SectionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RegisterRuns_Section",
                table: "RegisterRuns",
                sql: "\"SectionId\" IS NULL OR \"Kind\" = 'TaxMapControlRoll'");

            migrationBuilder.CreateIndex(
                name: "IX_SectionBoundaries_Geometry",
                table: "SectionBoundaries",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_SectionBoundaries_ImportBatchId",
                table: "SectionBoundaries",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SectionBoundaries_SectionId_EffectiveDate",
                table: "SectionBoundaries",
                columns: new[] { "SectionId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UX_SectionBoundaries_Current",
                table: "SectionBoundaries",
                column: "SectionId",
                unique: true,
                filter: "\"EndDate\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_RegisterRuns_TaxMapSections_SectionId",
                table: "RegisterRuns",
                column: "SectionId",
                principalTable: "TaxMapSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RegisterRuns_TaxMapSections_SectionId",
                table: "RegisterRuns");

            migrationBuilder.DropTable(
                name: "SectionBoundaries");

            migrationBuilder.DropIndex(
                name: "IX_RegisterRuns_SectionId",
                table: "RegisterRuns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RegisterRuns_Section",
                table: "RegisterRuns");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "RegisterRuns");
        }
    }
}
