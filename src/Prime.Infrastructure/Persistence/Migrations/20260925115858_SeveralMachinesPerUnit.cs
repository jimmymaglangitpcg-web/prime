using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeveralMachinesPerUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MachineryUnits_RpuId",
                table: "MachineryUnits");

            migrationBuilder.AddColumn<Guid>(
                name: "ActualUseId",
                table: "MachineryUnits",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClassificationId",
                table: "MachineryUnits",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachineryUnits_ActualUseId",
                table: "MachineryUnits",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryUnits_ClassificationId",
                table: "MachineryUnits",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryUnits_RpuId",
                table: "MachineryUnits",
                column: "RpuId");

            migrationBuilder.AddForeignKey(
                name: "FK_MachineryUnits_ActualUses_ActualUseId",
                table: "MachineryUnits",
                column: "ActualUseId",
                principalTable: "ActualUses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MachineryUnits_Classifications_ClassificationId",
                table: "MachineryUnits",
                column: "ClassificationId",
                principalTable: "Classifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MachineryUnits_ActualUses_ActualUseId",
                table: "MachineryUnits");

            migrationBuilder.DropForeignKey(
                name: "FK_MachineryUnits_Classifications_ClassificationId",
                table: "MachineryUnits");

            migrationBuilder.DropIndex(
                name: "IX_MachineryUnits_ActualUseId",
                table: "MachineryUnits");

            migrationBuilder.DropIndex(
                name: "IX_MachineryUnits_ClassificationId",
                table: "MachineryUnits");

            migrationBuilder.DropIndex(
                name: "IX_MachineryUnits_RpuId",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "ActualUseId",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "ClassificationId",
                table: "MachineryUnits");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryUnits_RpuId",
                table: "MachineryUnits",
                column: "RpuId",
                unique: true);
        }
    }
}
