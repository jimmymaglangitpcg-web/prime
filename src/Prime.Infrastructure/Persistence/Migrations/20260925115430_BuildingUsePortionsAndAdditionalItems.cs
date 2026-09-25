using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BuildingUsePortionsAndAdditionalItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BuildingUsePortionId",
                table: "BuildingComponents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdditionalItem",
                table: "BuildingComponents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BuildingUsePortions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FloorArea = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingUsePortions", x => x.Id);
                    table.CheckConstraint("CK_BuildingUsePortions_FloorArea", "\"FloorArea\" > 0");
                    table.CheckConstraint("CK_BuildingUsePortions_Sequence", "\"Sequence\" >= 1");
                    table.ForeignKey(
                        name: "FK_BuildingUsePortions_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BuildingUsePortions_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BuildingUsePortions_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingComponents_BuildingUsePortionId",
                table: "BuildingComponents",
                column: "BuildingUsePortionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BuildingComponents_AdditionalItemCost",
                table: "BuildingComponents",
                sql: "NOT \"IsAdditionalItem\" OR \"Cost\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingUsePortions_ActualUseId",
                table: "BuildingUsePortions",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingUsePortions_BuildingId_ClassificationId_ActualUseId",
                table: "BuildingUsePortions",
                columns: new[] { "BuildingId", "ClassificationId", "ActualUseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuildingUsePortions_BuildingId_Sequence",
                table: "BuildingUsePortions",
                columns: new[] { "BuildingId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuildingUsePortions_ClassificationId",
                table: "BuildingUsePortions",
                column: "ClassificationId");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingComponents_BuildingUsePortions_BuildingUsePortionId",
                table: "BuildingComponents",
                column: "BuildingUsePortionId",
                principalTable: "BuildingUsePortions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BuildingComponents_BuildingUsePortions_BuildingUsePortionId",
                table: "BuildingComponents");

            migrationBuilder.DropTable(
                name: "BuildingUsePortions");

            migrationBuilder.DropIndex(
                name: "IX_BuildingComponents_BuildingUsePortionId",
                table: "BuildingComponents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BuildingComponents_AdditionalItemCost",
                table: "BuildingComponents");

            migrationBuilder.DropColumn(
                name: "BuildingUsePortionId",
                table: "BuildingComponents");

            migrationBuilder.DropColumn(
                name: "IsAdditionalItem",
                table: "BuildingComponents");
        }
    }
}
