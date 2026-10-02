using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LamRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sex",
                table: "Taxpayers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CadastralNumber",
                table: "Property",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CadastralNumber",
                table: "Parcels",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SanggunianName",
                table: "Offices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailAddress",
                table: "NoticesOfAssessment",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SentDate",
                table: "NoticesOfAssessment",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EngineeringRegistrationDate",
                table: "MachineryUnits",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EngineeringRegistrationNumber",
                table: "MachineryUnits",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ImportPermitDate",
                table: "MachineryUnits",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportPermitNumber",
                table: "MachineryUnits",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReceiptDate",
                table: "MachineryUnits",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptNumber",
                table: "MachineryUnits",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierAddress",
                table: "MachineryUnits",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierName",
                table: "MachineryUnits",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReaLicenceNumber",
                table: "AppUsers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReaLicenceValidUntil",
                table: "AppUsers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatoryLicenceNumber",
                table: "ApprovalRecords",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SignatoryLicenceValidUntil",
                table: "ApprovalRecords",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SignedWithoutValidLicence",
                table: "ApprovalRecords",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresLicensedSignatory",
                table: "ApprovalChainSteps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_NoticesOfAssessment_Email",
                table: "NoticesOfAssessment",
                sql: "(\"ServiceMode\" = 'Email') <= (\"EmailAddress\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_NoticesOfAssessment_Email",
                table: "NoticesOfAssessment");

            migrationBuilder.DropColumn(
                name: "Sex",
                table: "Taxpayers");

            migrationBuilder.DropColumn(
                name: "CadastralNumber",
                table: "Property");

            migrationBuilder.DropColumn(
                name: "CadastralNumber",
                table: "Parcels");

            migrationBuilder.DropColumn(
                name: "SanggunianName",
                table: "Offices");

            migrationBuilder.DropColumn(
                name: "EmailAddress",
                table: "NoticesOfAssessment");

            migrationBuilder.DropColumn(
                name: "SentDate",
                table: "NoticesOfAssessment");

            migrationBuilder.DropColumn(
                name: "EngineeringRegistrationDate",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "EngineeringRegistrationNumber",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "ImportPermitDate",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "ImportPermitNumber",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "ReceiptDate",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "ReceiptNumber",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "SupplierAddress",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "SupplierName",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "ReaLicenceNumber",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "ReaLicenceValidUntil",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "SignatoryLicenceNumber",
                table: "ApprovalRecords");

            migrationBuilder.DropColumn(
                name: "SignatoryLicenceValidUntil",
                table: "ApprovalRecords");

            migrationBuilder.DropColumn(
                name: "SignedWithoutValidLicence",
                table: "ApprovalRecords");

            migrationBuilder.DropColumn(
                name: "RequiresLicensedSignatory",
                table: "ApprovalChainSteps");
        }
    }
}
