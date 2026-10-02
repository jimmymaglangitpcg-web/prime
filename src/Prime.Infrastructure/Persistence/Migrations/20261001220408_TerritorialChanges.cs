using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TerritorialChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DisputedAreas",
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
                    table.PrimaryKey("PK_DisputedAreas", x => x.Id);
                    table.CheckConstraint("CK_DisputedAreas_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" > \"EffectiveDate\"");
                });

            migrationBuilder.CreateTable(
                name: "PropertyBarangayParts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Area = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AssessedValueShare = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyBarangayParts", x => x.Id);
                    table.CheckConstraint("CK_PropertyBarangayParts_Area", "\"Area\" > 0");
                    table.CheckConstraint("CK_PropertyBarangayParts_Share", "\"AssessedValueShare\" >= 0 AND \"AssessedValueShare\" <= 100");
                    table.ForeignKey(
                        name: "FK_PropertyBarangayParts_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyBarangayParts_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TerritorialChangeJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PinMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RunStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TotalCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessedCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritorialChangeJobs", x => x.Id);
                    table.CheckConstraint("CK_TerritorialChangeJobs_Approval", "(\"Status\" = 'Approved') = (\"ApprovedAt\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "TerritorialChangeItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TerritorialChangeJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldPin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NewPin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritorialChangeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TerritorialChangeItems_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritorialChangeItems_TerritorialChangeJobs_TerritorialCha~",
                        column: x => x.TerritorialChangeJobId,
                        principalTable: "TerritorialChangeJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TerritorialChangeMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TerritorialChangeJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBarangayId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetBarangayId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritorialChangeMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TerritorialChangeMappings_Barangays_SourceBarangayId",
                        column: x => x.SourceBarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritorialChangeMappings_Barangays_TargetBarangayId",
                        column: x => x.TargetBarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritorialChangeMappings_TerritorialChangeJobs_Territorial~",
                        column: x => x.TerritorialChangeJobId,
                        principalTable: "TerritorialChangeJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisputedAreas_Code_EffectiveDate",
                table: "DisputedAreas",
                columns: new[] { "Code", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DisputedAreas_Geometry",
                table: "DisputedAreas",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_DisputedAreas_ImportBatchId",
                table: "DisputedAreas",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "UX_DisputedAreas_Current",
                table: "DisputedAreas",
                column: "Code",
                unique: true,
                filter: "\"EndDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyBarangayParts_BarangayId",
                table: "PropertyBarangayParts",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyBarangayParts_PropertyId_BarangayId",
                table: "PropertyBarangayParts",
                columns: new[] { "PropertyId", "BarangayId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TerritorialChangeItems_PropertyId",
                table: "TerritorialChangeItems",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritorialChangeItems_TerritorialChangeJobId_PropertyId",
                table: "TerritorialChangeItems",
                columns: new[] { "TerritorialChangeJobId", "PropertyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TerritorialChangeMappings_SourceBarangayId",
                table: "TerritorialChangeMappings",
                column: "SourceBarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritorialChangeMappings_TargetBarangayId",
                table: "TerritorialChangeMappings",
                column: "TargetBarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritorialChangeMappings_TerritorialChangeJobId_SourceBara~",
                table: "TerritorialChangeMappings",
                columns: new[] { "TerritorialChangeJobId", "SourceBarangayId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisputedAreas");

            migrationBuilder.DropTable(
                name: "PropertyBarangayParts");

            migrationBuilder.DropTable(
                name: "TerritorialChangeItems");

            migrationBuilder.DropTable(
                name: "TerritorialChangeMappings");

            migrationBuilder.DropTable(
                name: "TerritorialChangeJobs");
        }
    }
}
