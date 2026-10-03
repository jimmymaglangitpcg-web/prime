using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GeneralRevisionReviewAndBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Mode",
                table: "GeneralRevisionJobs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "GeneralRevisionJobs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InspectedOn",
                table: "GeneralRevisionItems",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InspectionFoundChanges",
                table: "GeneralRevisionItems",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectionNotes",
                table: "GeneralRevisionItems",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InspectionRecordedBy",
                table: "GeneralRevisionItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectionRoute",
                table: "GeneralRevisionItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InspectorId",
                table: "GeneralRevisionItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GeneralRevisionRunIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneralRevisionJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneralRevisionItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RpuNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Failed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralRevisionRunIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneralRevisionRunIssues_GeneralRevisionItems_GeneralRevisi~",
                        column: x => x.GeneralRevisionItemId,
                        principalTable: "GeneralRevisionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneralRevisionRunIssues_GeneralRevisionJobs_GeneralRevisio~",
                        column: x => x.GeneralRevisionJobId,
                        principalTable: "GeneralRevisionJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionItems_GeneralRevisionProgrammeId_InspectorId",
                table: "GeneralRevisionItems",
                columns: new[] { "GeneralRevisionProgrammeId", "InspectorId" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionItems_InspectorId",
                table: "GeneralRevisionItems",
                column: "InspectorId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionRunIssues_GeneralRevisionItemId",
                table: "GeneralRevisionRunIssues",
                column: "GeneralRevisionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralRevisionRunIssues_GeneralRevisionJobId_Pin",
                table: "GeneralRevisionRunIssues",
                columns: new[] { "GeneralRevisionJobId", "Pin" });

            migrationBuilder.AddForeignKey(
                name: "FK_GeneralRevisionItems_AppUsers_InspectorId",
                table: "GeneralRevisionItems",
                column: "InspectorId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GeneralRevisionItems_AppUsers_InspectorId",
                table: "GeneralRevisionItems");

            migrationBuilder.DropTable(
                name: "GeneralRevisionRunIssues");

            migrationBuilder.DropIndex(
                name: "IX_GeneralRevisionItems_GeneralRevisionProgrammeId_InspectorId",
                table: "GeneralRevisionItems");

            migrationBuilder.DropIndex(
                name: "IX_GeneralRevisionItems_InspectorId",
                table: "GeneralRevisionItems");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "GeneralRevisionJobs");

            migrationBuilder.DropColumn(
                name: "InspectedOn",
                table: "GeneralRevisionItems");

            migrationBuilder.DropColumn(
                name: "InspectionFoundChanges",
                table: "GeneralRevisionItems");

            migrationBuilder.DropColumn(
                name: "InspectionNotes",
                table: "GeneralRevisionItems");

            migrationBuilder.DropColumn(
                name: "InspectionRecordedBy",
                table: "GeneralRevisionItems");

            migrationBuilder.DropColumn(
                name: "InspectionRoute",
                table: "GeneralRevisionItems");

            migrationBuilder.DropColumn(
                name: "InspectorId",
                table: "GeneralRevisionItems");

            migrationBuilder.AlterColumn<string>(
                name: "Mode",
                table: "GeneralRevisionJobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);
        }
    }
}
