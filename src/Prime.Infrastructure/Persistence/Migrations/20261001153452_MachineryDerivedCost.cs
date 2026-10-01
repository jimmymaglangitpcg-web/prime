using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MachineryDerivedCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcquisitionCurrency",
                table: "MachineryUnits",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateInstalled",
                table: "MachineryUnits",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ForeignAcquisitionCost",
                table: "MachineryUnits",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsImported",
                table: "MachineryUnits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsInOperation",
                table: "MachineryUnits",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginCountry",
                table: "MachineryUnits",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceIndexSeries",
                table: "MachineryUnits",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RateDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PesosPerUnit = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                    table.CheckConstraint("CK_ExchangeRates_Approval", "(\"Status\" = 'Approved') = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_ExchangeRates_Rate", "\"PesosPerUnit\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "MachineryCostItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineryCostItems", x => x.Id);
                    table.CheckConstraint("CK_MachineryCostItems_Amount", "\"Amount\" >= 0");
                    table.ForeignKey(
                        name: "FK_MachineryCostItems_MachineryUnits_MachineryId",
                        column: x => x.MachineryId,
                        principalTable: "MachineryUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PriceIndices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Series = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceIndices", x => x.Id);
                    table.CheckConstraint("CK_PriceIndices_Approval", "(\"Status\" = 'Approved') = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_PriceIndices_Value", "\"Value\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "UX_ExchangeRates_Approved",
                table: "ExchangeRates",
                columns: new[] { "Currency", "RateDate" },
                unique: true,
                filter: "\"Status\" = 'Approved'");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryCostItems_MachineryId_Sequence",
                table: "MachineryCostItems",
                columns: new[] { "MachineryId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PriceIndices_Approved",
                table: "PriceIndices",
                columns: new[] { "Series", "Year" },
                unique: true,
                filter: "\"Status\" = 'Approved'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "MachineryCostItems");

            migrationBuilder.DropTable(
                name: "PriceIndices");

            migrationBuilder.DropColumn(
                name: "AcquisitionCurrency",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "DateInstalled",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "ForeignAcquisitionCost",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "IsImported",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "IsInOperation",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "OriginCountry",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "PriceIndexSeries",
                table: "MachineryUnits");
        }
    }
}
