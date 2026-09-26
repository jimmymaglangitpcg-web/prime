using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PinAssignedByTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedByTransactionId",
                table: "PinAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PinAssignments_AssignedByTransactionId",
                table: "PinAssignments",
                column: "AssignedByTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_PinAssignments_PropertyTransactions_AssignedByTransactionId",
                table: "PinAssignments",
                column: "AssignedByTransactionId",
                principalTable: "PropertyTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PinAssignments_PropertyTransactions_AssignedByTransactionId",
                table: "PinAssignments");

            migrationBuilder.DropIndex(
                name: "IX_PinAssignments_AssignedByTransactionId",
                table: "PinAssignments");

            migrationBuilder.DropColumn(
                name: "AssignedByTransactionId",
                table: "PinAssignments");
        }
    }
}
