using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FaasIdentityAndUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_PropertyTaxpayers_CurrentUnknownOwner",
                table: "PropertyTaxpayers");

            migrationBuilder.AddColumn<Guid>(
                name: "AssessmentId",
                table: "TaxDeclarations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HostRpuId",
                table: "RealPropertyUnit",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LandRpuId",
                table: "RealPropertyUnit",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PinSuffix",
                table: "RealPropertyUnit",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferRpuId",
                table: "PropertyTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RpuId",
                table: "PropertyTaxpayers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_AssessmentId",
                table: "TaxDeclarations",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RealPropertyUnit_HostRpuId",
                table: "RealPropertyUnit",
                column: "HostRpuId");

            migrationBuilder.CreateIndex(
                name: "IX_RealPropertyUnit_LandRpuId",
                table: "RealPropertyUnit",
                column: "LandRpuId");

            migrationBuilder.CreateIndex(
                name: "UX_RealPropertyUnit_Property_PinSuffix",
                table: "RealPropertyUnit",
                columns: new[] { "PropertyId", "PinSuffix" },
                unique: true,
                filter: "\"PinSuffix\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RealPropertyUnit_HostNotSelf",
                table: "RealPropertyUnit",
                sql: "\"HostRpuId\" IS NULL OR \"HostRpuId\" <> \"Id\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RealPropertyUnit_LandNotSelf",
                table: "RealPropertyUnit",
                sql: "\"LandRpuId\" IS NULL OR \"LandRpuId\" <> \"Id\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RealPropertyUnit_PinSuffix",
                table: "RealPropertyUnit",
                sql: "\"PinSuffix\" IS NULL OR \"PinSuffix\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactions_TransferRpuId",
                table: "PropertyTransactions",
                column: "TransferRpuId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTaxpayers_RpuId",
                table: "PropertyTaxpayers",
                column: "RpuId");

            migrationBuilder.CreateIndex(
                name: "UX_PropertyTaxpayers_CurrentUnknownOwner",
                table: "PropertyTaxpayers",
                columns: new[] { "PropertyId", "RpuId" },
                unique: true,
                filter: "\"Role\" = 'UnknownOwner' AND \"IsCurrent\"")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyTaxpayers_RealPropertyUnit_RpuId",
                table: "PropertyTaxpayers",
                column: "RpuId",
                principalTable: "RealPropertyUnit",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyTransactions_RealPropertyUnit_TransferRpuId",
                table: "PropertyTransactions",
                column: "TransferRpuId",
                principalTable: "RealPropertyUnit",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RealPropertyUnit_RealPropertyUnit_HostRpuId",
                table: "RealPropertyUnit",
                column: "HostRpuId",
                principalTable: "RealPropertyUnit",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RealPropertyUnit_RealPropertyUnit_LandRpuId",
                table: "RealPropertyUnit",
                column: "LandRpuId",
                principalTable: "RealPropertyUnit",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaxDeclarations_Assessments_AssessmentId",
                table: "TaxDeclarations",
                column: "AssessmentId",
                principalTable: "Assessments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PropertyTaxpayers_RealPropertyUnit_RpuId",
                table: "PropertyTaxpayers");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyTransactions_RealPropertyUnit_TransferRpuId",
                table: "PropertyTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_RealPropertyUnit_RealPropertyUnit_HostRpuId",
                table: "RealPropertyUnit");

            migrationBuilder.DropForeignKey(
                name: "FK_RealPropertyUnit_RealPropertyUnit_LandRpuId",
                table: "RealPropertyUnit");

            migrationBuilder.DropForeignKey(
                name: "FK_TaxDeclarations_Assessments_AssessmentId",
                table: "TaxDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_TaxDeclarations_AssessmentId",
                table: "TaxDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_RealPropertyUnit_HostRpuId",
                table: "RealPropertyUnit");

            migrationBuilder.DropIndex(
                name: "IX_RealPropertyUnit_LandRpuId",
                table: "RealPropertyUnit");

            migrationBuilder.DropIndex(
                name: "UX_RealPropertyUnit_Property_PinSuffix",
                table: "RealPropertyUnit");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RealPropertyUnit_HostNotSelf",
                table: "RealPropertyUnit");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RealPropertyUnit_LandNotSelf",
                table: "RealPropertyUnit");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RealPropertyUnit_PinSuffix",
                table: "RealPropertyUnit");

            migrationBuilder.DropIndex(
                name: "IX_PropertyTransactions_TransferRpuId",
                table: "PropertyTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PropertyTaxpayers_RpuId",
                table: "PropertyTaxpayers");

            migrationBuilder.DropIndex(
                name: "UX_PropertyTaxpayers_CurrentUnknownOwner",
                table: "PropertyTaxpayers");

            migrationBuilder.DropColumn(
                name: "AssessmentId",
                table: "TaxDeclarations");

            migrationBuilder.DropColumn(
                name: "HostRpuId",
                table: "RealPropertyUnit");

            migrationBuilder.DropColumn(
                name: "LandRpuId",
                table: "RealPropertyUnit");

            migrationBuilder.DropColumn(
                name: "PinSuffix",
                table: "RealPropertyUnit");

            migrationBuilder.DropColumn(
                name: "TransferRpuId",
                table: "PropertyTransactions");

            migrationBuilder.DropColumn(
                name: "RpuId",
                table: "PropertyTaxpayers");

            migrationBuilder.CreateIndex(
                name: "UX_PropertyTaxpayers_CurrentUnknownOwner",
                table: "PropertyTaxpayers",
                column: "PropertyId",
                unique: true,
                filter: "\"Role\" = 'UnknownOwner' AND \"IsCurrent\"");
        }
    }
}
