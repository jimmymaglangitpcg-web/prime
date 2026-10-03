using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GeneralRevisionProgrammes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GeneralRevisionProgrammeId",
                table: "GeneralRevisionJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "GeneralRevisionJobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GeneralRevisionProgrammes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionYear = table.Column<int>(type: "integer", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SmvId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfficeOrderReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OrdinanceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralRevisionProgrammes", x => x.Id);
                    table.CheckConstraint("CK_GeneralRevisionProgrammes_Cancelled", "(\"Status\" = 'Cancelled') = (\"CancellationReason\" IS NOT NULL)");
                    table.CheckConstraint("CK_GeneralRevisionProgrammes_Completed", "(\"Status\" = 'Completed') = (\"CompletedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_GeneralRevisionProgrammes_Smvs_SmvId",
                        column: x => x.SmvId,
                        principalTable: "Smvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GeneralRevisionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneralRevisionProgrammeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RpuId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RpuNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RpuType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PreviousAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousMarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PreviousAssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ValuationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewMarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NewAssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralRevisionItems", x => x.Id);
                    table.CheckConstraint("CK_GeneralRevisionItems_Failed", "(\"Status\" = 'Failed') = (\"FailureReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_GeneralRevisionItems_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneralRevisionItems_Assessments_PreviousAssessmentId",
                        column: x => x.PreviousAssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneralRevisionItems_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneralRevisionItems_GeneralRevisionJobs_LastRunId",
                        column: x => x.LastRunId,
                        principalTable: "GeneralRevisionJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneralRevisionItems_GeneralRevisionProgrammes_GeneralRevis~",
                        column: x => x.GeneralRevisionProgrammeId,
                        principalTable: "GeneralRevisionProgrammes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneralRevisionItems_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneralRevisionItems_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneralRevisionItems_RealPropertyUnit_RpuId",
                        column: x => x.RpuId,
                        principalTable: "RealPropertyUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneralRevisionItems_Valuations_ValuationId",
                        column: x => x.ValuationId,
                        principalTable: "Valuations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GeneralRevisionScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneralRevisionProgrammeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralRevisionScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneralRevisionScopes_GeneralRevisionProgrammes_GeneralRevi~",
                        column: x => x.GeneralRevisionProgrammeId,
                        principalTable: "GeneralRevisionProgrammes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GeneralRevisionScopes_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GeneralRevisionSuspensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneralRevisionProgrammeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UntilDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Reference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LiftedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralRevisionSuspensions", x => x.Id);
                    table.CheckConstraint("CK_GeneralRevisionSuspensions_Dates", "(\"UntilDate\" IS NULL OR \"UntilDate\" >= \"FromDate\") AND (\"LiftedOn\" IS NULL OR \"LiftedOn\" >= \"FromDate\")");
                    table.ForeignKey(
                        name: "FK_GeneralRevisionSuspensions_GeneralRevisionProgrammes_Genera~",
                        column: x => x.GeneralRevisionProgrammeId,
                        principalTable: "GeneralRevisionProgrammes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionJobs_GeneralRevisionProgrammeId",
                table: "GeneralRevisionJobs",
                column: "GeneralRevisionProgrammeId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionItems_AssessmentId",
                table: "GeneralRevisionItems",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionItems_BarangayId",
                table: "GeneralRevisionItems",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionItems_GeneralRevisionProgrammeId_BarangayId_~",
                table: "GeneralRevisionItems",
                columns: new[] { "GeneralRevisionProgrammeId", "BarangayId", "Pin" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionItems_GeneralRevisionProgrammeId_RpuId",
                table: "GeneralRevisionItems",
                columns: new[] { "GeneralRevisionProgrammeId", "RpuId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionItems_GeneralRevisionProgrammeId_Status",
                table: "GeneralRevisionItems",
                columns: new[] { "GeneralRevisionProgrammeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionItems_LastRunId",
                table: "GeneralRevisionItems",
                column: "LastRunId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionItems_MunicipalityId",
                table: "GeneralRevisionItems",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionItems_PreviousAssessmentId",
                table: "GeneralRevisionItems",
                column: "PreviousAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionItems_PropertyId",
                table: "GeneralRevisionItems",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionItems_RpuId",
                table: "GeneralRevisionItems",
                column: "RpuId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionItems_ValuationId",
                table: "GeneralRevisionItems",
                column: "ValuationId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionProgrammes_RevisionYear",
                table: "GeneralRevisionProgrammes",
                column: "RevisionYear");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionProgrammes_SmvId",
                table: "GeneralRevisionProgrammes",
                column: "SmvId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionScopes_GeneralRevisionProgrammeId_Municipali~",
                table: "GeneralRevisionScopes",
                columns: new[] { "GeneralRevisionProgrammeId", "MunicipalityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionScopes_MunicipalityId",
                table: "GeneralRevisionScopes",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionSuspensions_GeneralRevisionProgrammeId",
                table: "GeneralRevisionSuspensions",
                column: "GeneralRevisionProgrammeId");

            migrationBuilder.AddForeignKey(
                name: "FK_GeneralRevisionJobs_GeneralRevisionProgrammes_GeneralRevisi~",
                table: "GeneralRevisionJobs",
                column: "GeneralRevisionProgrammeId",
                principalTable: "GeneralRevisionProgrammes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GeneralRevisionJobs_GeneralRevisionProgrammes_GeneralRevisi~",
                table: "GeneralRevisionJobs");

            migrationBuilder.DropTable(
                name: "GeneralRevisionItems");

            migrationBuilder.DropTable(
                name: "GeneralRevisionScopes");

            migrationBuilder.DropTable(
                name: "GeneralRevisionSuspensions");

            migrationBuilder.DropTable(
                name: "GeneralRevisionProgrammes");

            migrationBuilder.DropIndex(
                name: "IX_GeneralRevisionJobs_GeneralRevisionProgrammeId",
                table: "GeneralRevisionJobs");

            migrationBuilder.DropColumn(
                name: "GeneralRevisionProgrammeId",
                table: "GeneralRevisionJobs");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "GeneralRevisionJobs");
        }
    }
}
