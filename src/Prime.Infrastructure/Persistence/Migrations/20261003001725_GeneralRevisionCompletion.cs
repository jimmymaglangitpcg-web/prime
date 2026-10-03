using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GeneralRevisionCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExclusionReason",
                table: "GeneralRevisionItems",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GeneralRevisionChecklistStepDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Gate = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
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
                    table.PrimaryKey("PK_GeneralRevisionChecklistStepDefinitions", x => x.Id);
                    table.CheckConstraint("CK_GeneralRevisionChecklistStepDefinitions_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_GeneralRevisionChecklistStepDefinitions_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
                    table.CheckConstraint("CK_GeneralRevisionChecklistStepDefinitions_Sequence", "\"Sequence\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "GeneralRevisionChecklistSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneralRevisionProgrammeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Gate = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CompletedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Evidence = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralRevisionChecklistSteps", x => x.Id);
                    table.CheckConstraint("CK_GeneralRevisionChecklistSteps_Manual", "\"Gate\" IS NULL OR \"CompletedOn\" IS NULL");
                    table.ForeignKey(
                        name: "FK_GeneralRevisionChecklistSteps_GeneralRevisionChecklistStepD~",
                        column: x => x.DefinitionId,
                        principalTable: "GeneralRevisionChecklistStepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneralRevisionChecklistSteps_GeneralRevisionProgrammes_Gen~",
                        column: x => x.GeneralRevisionProgrammeId,
                        principalTable: "GeneralRevisionProgrammes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_GeneralRevisionItems_Excluded",
                table: "GeneralRevisionItems",
                sql: "(\"Status\" = 'Excluded') = (\"ExclusionReason\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionChecklistStepDefinitions_Code_EffectiveDate",
                table: "GeneralRevisionChecklistStepDefinitions",
                columns: new[] { "Code", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionChecklistStepDefinitions_Status_EffectiveDate",
                table: "GeneralRevisionChecklistStepDefinitions",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UX_GeneralRevisionChecklistStepDefinitions_OpenApproved",
                table: "GeneralRevisionChecklistStepDefinitions",
                column: "Code",
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionChecklistSteps_DefinitionId",
                table: "GeneralRevisionChecklistSteps",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionChecklistSteps_GeneralRevisionProgrammeId_Co~",
                table: "GeneralRevisionChecklistSteps",
                columns: new[] { "GeneralRevisionProgrammeId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeneralRevisionChecklistSteps");

            migrationBuilder.DropTable(
                name: "GeneralRevisionChecklistStepDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GeneralRevisionItems_Excluded",
                table: "GeneralRevisionItems");

            migrationBuilder.DropColumn(
                name: "ExclusionReason",
                table: "GeneralRevisionItems");
        }
    }
}
