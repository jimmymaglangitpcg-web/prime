using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaxMappingTieUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FieldConfirmedAt",
                table: "PinAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FieldConfirmedBy",
                table: "PinAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OfficeTieUpAt",
                table: "PinAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OfficeTieUpBy",
                table: "PinAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TieUpRemarks",
                table: "PinAssignments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PinAssignments_TieUp",
                table: "PinAssignments",
                sql: "(\"OfficeTieUpAt\" IS NULL OR \"Kind\" = 'Temporary') AND (\"FieldConfirmedAt\" IS NULL OR \"OfficeTieUpAt\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PinAssignments_TieUp",
                table: "PinAssignments");

            migrationBuilder.DropColumn(
                name: "FieldConfirmedAt",
                table: "PinAssignments");

            migrationBuilder.DropColumn(
                name: "FieldConfirmedBy",
                table: "PinAssignments");

            migrationBuilder.DropColumn(
                name: "OfficeTieUpAt",
                table: "PinAssignments");

            migrationBuilder.DropColumn(
                name: "OfficeTieUpBy",
                table: "PinAssignments");

            migrationBuilder.DropColumn(
                name: "TieUpRemarks",
                table: "PinAssignments");
        }
    }
}
