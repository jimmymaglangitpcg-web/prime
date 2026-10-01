using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OfficeApprovalRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ApprovalChains_OpenApproved",
                table: "ApprovalChains");

            migrationBuilder.AddColumn<Guid>(
                name: "DelegationId",
                table: "ApprovalRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SignerOfficeId",
                table: "ApprovalRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnderDelegation",
                table: "ApprovalRecords",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFinalApproval",
                table: "ApprovalChainSteps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RequiredRole",
                table: "ApprovalChainSteps",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignerOffice",
                table: "ApprovalChainSteps",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Any");

            migrationBuilder.AddColumn<Guid>(
                name: "OfficeId",
                table: "ApprovalChains",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRecords_DelegationId",
                table: "ApprovalRecords",
                column: "DelegationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalChains_OfficeId",
                table: "ApprovalChains",
                column: "OfficeId");

            migrationBuilder.CreateIndex(
                name: "UX_ApprovalChains_OpenApproved",
                table: "ApprovalChains",
                columns: new[] { "SubjectType", "OfficeId" },
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalChains_Offices_OfficeId",
                table: "ApprovalChains",
                column: "OfficeId",
                principalTable: "Offices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalRecords_ApprovalDelegations_DelegationId",
                table: "ApprovalRecords",
                column: "DelegationId",
                principalTable: "ApprovalDelegations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalChains_Offices_OfficeId",
                table: "ApprovalChains");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalRecords_ApprovalDelegations_DelegationId",
                table: "ApprovalRecords");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalRecords_DelegationId",
                table: "ApprovalRecords");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalChains_OfficeId",
                table: "ApprovalChains");

            migrationBuilder.DropIndex(
                name: "UX_ApprovalChains_OpenApproved",
                table: "ApprovalChains");

            migrationBuilder.DropColumn(
                name: "DelegationId",
                table: "ApprovalRecords");

            migrationBuilder.DropColumn(
                name: "SignerOfficeId",
                table: "ApprovalRecords");

            migrationBuilder.DropColumn(
                name: "UnderDelegation",
                table: "ApprovalRecords");

            migrationBuilder.DropColumn(
                name: "IsFinalApproval",
                table: "ApprovalChainSteps");

            migrationBuilder.DropColumn(
                name: "RequiredRole",
                table: "ApprovalChainSteps");

            migrationBuilder.DropColumn(
                name: "SignerOffice",
                table: "ApprovalChainSteps");

            migrationBuilder.DropColumn(
                name: "OfficeId",
                table: "ApprovalChains");

            migrationBuilder.CreateIndex(
                name: "UX_ApprovalChains_OpenApproved",
                table: "ApprovalChains",
                column: "SubjectType",
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL");
        }
    }
}
