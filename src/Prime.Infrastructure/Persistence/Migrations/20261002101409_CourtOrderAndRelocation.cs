using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CourtOrderAndRelocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RestoresTaxDeclarationId",
                table: "TaxDeclarations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelocatedRpuId",
                table: "PropertyTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_RestoresTaxDeclarationId",
                table: "TaxDeclarations",
                column: "RestoresTaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactions_RelocatedRpuId",
                table: "PropertyTransactions",
                column: "RelocatedRpuId");

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyTransactions_RealPropertyUnit_RelocatedRpuId",
                table: "PropertyTransactions",
                column: "RelocatedRpuId",
                principalTable: "RealPropertyUnit",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaxDeclarations_TaxDeclarations_RestoresTaxDeclarationId",
                table: "TaxDeclarations",
                column: "RestoresTaxDeclarationId",
                principalTable: "TaxDeclarations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PropertyTransactions_RealPropertyUnit_RelocatedRpuId",
                table: "PropertyTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_TaxDeclarations_TaxDeclarations_RestoresTaxDeclarationId",
                table: "TaxDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_TaxDeclarations_RestoresTaxDeclarationId",
                table: "TaxDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_PropertyTransactions_RelocatedRpuId",
                table: "PropertyTransactions");

            migrationBuilder.DropColumn(
                name: "RestoresTaxDeclarationId",
                table: "TaxDeclarations");

            migrationBuilder.DropColumn(
                name: "RelocatedRpuId",
                table: "PropertyTransactions");
        }
    }
}
