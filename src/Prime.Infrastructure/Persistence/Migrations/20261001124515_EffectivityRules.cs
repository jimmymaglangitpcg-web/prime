using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EffectivityRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CauseWindowDays",
                table: "TransactionTypes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EffectivityLegalBasis",
                table: "TransactionTypes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EffectivityRule",
                table: "TransactionTypes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CauseDate",
                table: "Assessments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CauseWindowDays",
                table: "Assessments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CauseWindowExceeded",
                table: "Assessments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EffectivityOverrideReason",
                table: "Assessments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EffectivityRule",
                table: "Assessments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MadeOn",
                table: "Assessments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionCode",
                table: "Assessments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransactionTypeId",
                table: "Assessments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EffectivityQuarter",
                table: "Assessments",
                type: "integer",
                nullable: false,
                computedColumnSql: "(EXTRACT(QUARTER FROM \"EffectiveDate\"))::integer",
                stored: true);

            migrationBuilder.AddColumn<int>(
                name: "EffectivityYear",
                table: "Assessments",
                type: "integer",
                nullable: false,
                computedColumnSql: "(EXTRACT(YEAR FROM \"EffectiveDate\"))::integer",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_TransactionTypeId",
                table: "Assessments",
                column: "TransactionTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assessments_TransactionTypes_TransactionTypeId",
                table: "Assessments",
                column: "TransactionTypeId",
                principalTable: "TransactionTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assessments_TransactionTypes_TransactionTypeId",
                table: "Assessments");

            migrationBuilder.DropIndex(
                name: "IX_Assessments_TransactionTypeId",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "EffectivityQuarter",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "EffectivityYear",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "CauseWindowDays",
                table: "TransactionTypes");

            migrationBuilder.DropColumn(
                name: "EffectivityLegalBasis",
                table: "TransactionTypes");

            migrationBuilder.DropColumn(
                name: "EffectivityRule",
                table: "TransactionTypes");

            migrationBuilder.DropColumn(
                name: "CauseDate",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "CauseWindowDays",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "CauseWindowExceeded",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "EffectivityOverrideReason",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "EffectivityRule",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "MadeOn",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "TransactionCode",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "TransactionTypeId",
                table: "Assessments");
        }
    }
}
