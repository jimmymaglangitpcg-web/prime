using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrfPastOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludePastOwners",
                table: "RegisterRuns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_RegisterRuns_PastOwners",
                table: "RegisterRuns",
                sql: "NOT \"IncludePastOwners\" OR \"Kind\" = 'OwnershipRecordCard'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RegisterRuns_PastOwners",
                table: "RegisterRuns");

            migrationBuilder.DropColumn(
                name: "IncludePastOwners",
                table: "RegisterRuns");
        }
    }
}
