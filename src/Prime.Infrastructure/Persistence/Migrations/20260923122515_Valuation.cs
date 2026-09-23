using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Valuation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropertyTypes",
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
                    table.PrimaryKey("PK_PropertyTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Smvs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrdinanceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrdinanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ApprovalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectivityDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RevisionYear = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Smvs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrdinanceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrdinanceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LowerValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UpperValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AssessmentPercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentLevels_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentLevels_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentLevels_PropertyTypes_PropertyTypeId",
                        column: x => x.PropertyTypeId,
                        principalTable: "PropertyTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SmvSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaximumValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmvSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmvSchedules_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSchedules_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSchedules_PropertyTypes_PropertyTypeId",
                        column: x => x.PropertyTypeId,
                        principalTable: "PropertyTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSchedules_Smvs_SmvId",
                        column: x => x.SmvId,
                        principalTable: "Smvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSchedules_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Valuations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RpuId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvId = table.Column<Guid>(type: "uuid", nullable: true),
                    SmvScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValuationMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ComputedMarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BreakdownJson = table.Column<string>(type: "jsonb", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Valuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Valuations_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Valuations_RealPropertyUnit_RpuId",
                        column: x => x.RpuId,
                        principalTable: "RealPropertyUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Valuations_SmvSchedules_SmvScheduleId",
                        column: x => x.SmvScheduleId,
                        principalTable: "SmvSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Valuations_Smvs_SmvId",
                        column: x => x.SmvId,
                        principalTable: "Smvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentLevels_ActualUseId",
                table: "AssessmentLevels",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentLevels_ClassificationId_ActualUseId_PropertyTypeI~",
                table: "AssessmentLevels",
                columns: new[] { "ClassificationId", "ActualUseId", "PropertyTypeId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentLevels_PropertyTypeId",
                table: "AssessmentLevels",
                column: "PropertyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTypes_Code",
                table: "PropertyTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Smvs_EffectivityDate",
                table: "Smvs",
                column: "EffectivityDate");

            migrationBuilder.CreateIndex(
                name: "IX_Smvs_OrdinanceNumber",
                table: "Smvs",
                column: "OrdinanceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmvSchedules_ActualUseId",
                table: "SmvSchedules",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSchedules_ClassificationId_ActualUseId_PropertyTypeId_Zo~",
                table: "SmvSchedules",
                columns: new[] { "ClassificationId", "ActualUseId", "PropertyTypeId", "ZoneId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SmvSchedules_PropertyTypeId",
                table: "SmvSchedules",
                column: "PropertyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSchedules_SmvId",
                table: "SmvSchedules",
                column: "SmvId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSchedules_ZoneId",
                table: "SmvSchedules",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Valuations_PropertyId",
                table: "Valuations",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Valuations_RpuId",
                table: "Valuations",
                column: "RpuId");

            migrationBuilder.CreateIndex(
                name: "IX_Valuations_SmvId",
                table: "Valuations",
                column: "SmvId");

            migrationBuilder.CreateIndex(
                name: "IX_Valuations_SmvScheduleId",
                table: "Valuations",
                column: "SmvScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Valuations_SourceType_SourceId_ComputedAt",
                table: "Valuations",
                columns: new[] { "SourceType", "SourceId", "ComputedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentLevels");

            migrationBuilder.DropTable(
                name: "Valuations");

            migrationBuilder.DropTable(
                name: "SmvSchedules");

            migrationBuilder.DropTable(
                name: "PropertyTypes");

            migrationBuilder.DropTable(
                name: "Smvs");
        }
    }
}
