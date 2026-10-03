using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GeneralRevisionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GeneralRevisionProgrammeId",
                table: "RegisterRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RollGateOverrideReason",
                table: "RegisterRuns",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegisterRuns_GeneralRevisionProgrammeId",
                table: "RegisterRuns",
                column: "GeneralRevisionProgrammeId");

            migrationBuilder.AddForeignKey(
                name: "FK_RegisterRuns_GeneralRevisionProgrammes_GeneralRevisionProgr~",
                table: "RegisterRuns",
                column: "GeneralRevisionProgrammeId",
                principalTable: "GeneralRevisionProgrammes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RegisterRuns_GeneralRevisionProgrammes_GeneralRevisionProgr~",
                table: "RegisterRuns");

            migrationBuilder.DropIndex(
                name: "IX_RegisterRuns_GeneralRevisionProgrammeId",
                table: "RegisterRuns");

            migrationBuilder.DropColumn(
                name: "GeneralRevisionProgrammeId",
                table: "RegisterRuns");

            migrationBuilder.DropColumn(
                name: "RollGateOverrideReason",
                table: "RegisterRuns");
        }
    }
}
