using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SmvAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Smvs_CertifiedBasis",
                table: "Smvs");

            migrationBuilder.AddColumn<string>(
                name: "AmendmentGround",
                table: "Smvs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AmendsSmvId",
                table: "Smvs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Smvs_AmendsSmvId",
                table: "Smvs",
                column: "AmendsSmvId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Smvs_AmendmentBasis",
                table: "Smvs",
                sql: "(\"Basis\" = 'Amendment') = (\"AmendsSmvId\" IS NOT NULL AND \"AmendmentGround\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Smvs_CertifiedBasis",
                table: "Smvs",
                sql: "\"Basis\" NOT IN ('Certified', 'Amendment') OR \"CertificationReference\" IS NOT NULL OR \"Status\" IN ('Draft', 'Cancelled')");

            migrationBuilder.AddForeignKey(
                name: "FK_Smvs_Smvs_AmendsSmvId",
                table: "Smvs",
                column: "AmendsSmvId",
                principalTable: "Smvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Smvs_Smvs_AmendsSmvId",
                table: "Smvs");

            migrationBuilder.DropIndex(
                name: "IX_Smvs_AmendsSmvId",
                table: "Smvs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Smvs_AmendmentBasis",
                table: "Smvs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Smvs_CertifiedBasis",
                table: "Smvs");

            migrationBuilder.DropColumn(
                name: "AmendmentGround",
                table: "Smvs");

            migrationBuilder.DropColumn(
                name: "AmendsSmvId",
                table: "Smvs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Smvs_CertifiedBasis",
                table: "Smvs",
                sql: "\"Basis\" <> 'Certified' OR \"CertificationReference\" IS NOT NULL OR \"Status\" IN ('Draft', 'Cancelled')");
        }
    }
}
