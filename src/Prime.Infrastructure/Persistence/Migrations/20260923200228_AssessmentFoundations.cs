using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssessmentFoundations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MachineryUnits_RpuId",
                table: "MachineryUnits");

            migrationBuilder.DropIndex(
                name: "IX_Lands_RpuId",
                table: "Lands");

            migrationBuilder.DropIndex(
                name: "IX_Buildings_RpuId",
                table: "Buildings");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryUnits_RpuId",
                table: "MachineryUnits",
                column: "RpuId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lands_RpuId",
                table: "Lands",
                column: "RpuId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_RpuId",
                table: "Buildings",
                column: "RpuId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MachineryUnits_RpuId",
                table: "MachineryUnits");

            migrationBuilder.DropIndex(
                name: "IX_Lands_RpuId",
                table: "Lands");

            migrationBuilder.DropIndex(
                name: "IX_Buildings_RpuId",
                table: "Buildings");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryUnits_RpuId",
                table: "MachineryUnits",
                column: "RpuId");

            migrationBuilder.CreateIndex(
                name: "IX_Lands_RpuId",
                table: "Lands",
                column: "RpuId");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_RpuId",
                table: "Buildings",
                column: "RpuId");
        }
    }
}
