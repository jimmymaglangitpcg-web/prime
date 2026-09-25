using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LandStripsImprovementsAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImprovementKindId",
                table: "SmvSchedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdjustmentFactors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdjustmentFactors", x => x.Id);
                    table.CheckConstraint("CK_AdjustmentFactors_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_AdjustmentFactors_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
                    table.CheckConstraint("CK_AdjustmentFactors_Percent", "\"Percent\" > -100 AND \"Percent\" <= 1000");
                    table.ForeignKey(
                        name: "FK_AdjustmentFactors_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdjustmentFactors_Smvs_SmvId",
                        column: x => x.SmvId,
                        principalTable: "Smvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImprovementKinds",
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
                    table.PrimaryKey("PK_ImprovementKinds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LandStrips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Area = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandStrips", x => x.Id);
                    table.CheckConstraint("CK_LandStrips_Area", "\"Area\" >= 0");
                    table.CheckConstraint("CK_LandStrips_Sequence", "\"Sequence\" >= 1");
                    table.ForeignKey(
                        name: "FK_LandStrips_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandStrips_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandStrips_Lands_LandId",
                        column: x => x.LandId,
                        principalTable: "Lands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandStrips_SubClassifications_SubClassificationId",
                        column: x => x.SubClassificationId,
                        principalTable: "SubClassifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandStrips_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LandImprovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ImprovementKindId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    IsProductive = table.Column<bool>(type: "boolean", nullable: true),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandImprovements", x => x.Id);
                    table.CheckConstraint("CK_LandImprovements_Quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_LandImprovements_Sequence", "\"Sequence\" >= 1");
                    table.ForeignKey(
                        name: "FK_LandImprovements_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandImprovements_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandImprovements_ImprovementKinds_ImprovementKindId",
                        column: x => x.ImprovementKindId,
                        principalTable: "ImprovementKinds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandImprovements_Lands_LandId",
                        column: x => x.LandId,
                        principalTable: "Lands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LandAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandId = table.Column<Guid>(type: "uuid", nullable: false),
                    LandStripId = table.Column<Guid>(type: "uuid", nullable: true),
                    FactorCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandAdjustments_LandStrips_LandStripId",
                        column: x => x.LandStripId,
                        principalTable: "LandStrips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandAdjustments_Lands_LandId",
                        column: x => x.LandId,
                        principalTable: "Lands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmvSchedules_ImprovementKindId",
                table: "SmvSchedules",
                column: "ImprovementKindId");

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentFactors_ClassificationId",
                table: "AdjustmentFactors",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentFactors_Status_EffectiveDate",
                table: "AdjustmentFactors",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UX_AdjustmentFactors_OpenApproved",
                table: "AdjustmentFactors",
                columns: new[] { "SmvId", "Code" },
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementKinds_Code",
                table: "ImprovementKinds",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandAdjustments_LandId_LandStripId_FactorCode",
                table: "LandAdjustments",
                columns: new[] { "LandId", "LandStripId", "FactorCode" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_LandAdjustments_LandStripId",
                table: "LandAdjustments",
                column: "LandStripId");

            migrationBuilder.CreateIndex(
                name: "IX_LandImprovements_ActualUseId",
                table: "LandImprovements",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_LandImprovements_ClassificationId",
                table: "LandImprovements",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_LandImprovements_ImprovementKindId",
                table: "LandImprovements",
                column: "ImprovementKindId");

            migrationBuilder.CreateIndex(
                name: "IX_LandImprovements_LandId_Sequence",
                table: "LandImprovements",
                columns: new[] { "LandId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandStrips_ActualUseId",
                table: "LandStrips",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_LandStrips_ClassificationId",
                table: "LandStrips",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_LandStrips_LandId_Sequence",
                table: "LandStrips",
                columns: new[] { "LandId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandStrips_SubClassificationId",
                table: "LandStrips",
                column: "SubClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_LandStrips_ZoneId",
                table: "LandStrips",
                column: "ZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_SmvSchedules_ImprovementKinds_ImprovementKindId",
                table: "SmvSchedules",
                column: "ImprovementKindId",
                principalTable: "ImprovementKinds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            // Each existing land becomes its own first strip (docs/analysis/mrpaao-forms-model.md §8.5). No value changes.
            migrationBuilder.Sql("""
                INSERT INTO "LandStrips" ("Id", "LandId", "Sequence", "ClassificationId", "SubClassificationId", "ActualUseId", "ZoneId", "Area",
                    "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy")
                SELECT gen_random_uuid(), l."Id", 1, l."ClassificationId", l."SubClassificationId", l."ActualUseId", NULL, l."Area",
                    now(), NULL, NULL, NULL
                FROM "Lands" l
                WHERE NOT EXISTS (SELECT 1 FROM "LandStrips" x WHERE x."LandId" = l."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SmvSchedules_ImprovementKinds_ImprovementKindId",
                table: "SmvSchedules");

            migrationBuilder.DropTable(
                name: "AdjustmentFactors");

            migrationBuilder.DropTable(
                name: "LandAdjustments");

            migrationBuilder.DropTable(
                name: "LandImprovements");

            migrationBuilder.DropTable(
                name: "LandStrips");

            migrationBuilder.DropTable(
                name: "ImprovementKinds");

            migrationBuilder.DropIndex(
                name: "IX_SmvSchedules_ImprovementKindId",
                table: "SmvSchedules");

            migrationBuilder.DropColumn(
                name: "ImprovementKindId",
                table: "SmvSchedules");
        }
    }
}
