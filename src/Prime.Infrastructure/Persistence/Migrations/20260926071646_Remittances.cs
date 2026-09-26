using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Remittances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RemittanceId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            // Model-only: Payment.Version maps to PostgreSQL's xmin system column; Npgsql
            // emits no DDL for it (the ParcelConcurrencyToken precedent).
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Payments",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "Remittances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RemittanceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CashierUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentCount = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionRemarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Remittances", x => x.Id);
                    table.CheckConstraint("CK_Remittances_Decision", "(\"Status\" = 'Submitted') = (\"DecidedAt\" IS NULL)");
                    table.CheckConstraint("CK_Remittances_Totals", "\"TotalAmount\" > 0 AND \"PaymentCount\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "RemittanceAccountTotals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RemittanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Fund = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemittanceAccountTotals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemittanceAccountTotals_Remittances_RemittanceId",
                        column: x => x.RemittanceId,
                        principalTable: "Remittances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemittanceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RemittanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemittanceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemittanceItems_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemittanceItems_Remittances_RemittanceId",
                        column: x => x.RemittanceId,
                        principalTable: "Remittances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemittanceModeTotals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RemittanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentModeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemittanceModeTotals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemittanceModeTotals_PaymentModes_PaymentModeId",
                        column: x => x.PaymentModeId,
                        principalTable: "PaymentModes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemittanceModeTotals_Remittances_RemittanceId",
                        column: x => x.RemittanceId,
                        principalTable: "Remittances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RemittanceId",
                table: "Payments",
                column: "RemittanceId");

            migrationBuilder.CreateIndex(
                name: "IX_RemittanceAccountTotals_RemittanceId",
                table: "RemittanceAccountTotals",
                column: "RemittanceId");

            migrationBuilder.CreateIndex(
                name: "IX_RemittanceItems_PaymentId",
                table: "RemittanceItems",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_RemittanceItems_RemittanceId_PaymentId",
                table: "RemittanceItems",
                columns: new[] { "RemittanceId", "PaymentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemittanceModeTotals_PaymentModeId",
                table: "RemittanceModeTotals",
                column: "PaymentModeId");

            migrationBuilder.CreateIndex(
                name: "IX_RemittanceModeTotals_RemittanceId_PaymentModeId",
                table: "RemittanceModeTotals",
                columns: new[] { "RemittanceId", "PaymentModeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Remittances_CollectionDate_CashierUserId",
                table: "Remittances",
                columns: new[] { "CollectionDate", "CashierUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Remittances_RemittanceNumber",
                table: "Remittances",
                column: "RemittanceNumber",
                unique: true,
                filter: "\"RemittanceNumber\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Remittances_RemittanceId",
                table: "Payments",
                column: "RemittanceId",
                principalTable: "Remittances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Remittances_RemittanceId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "RemittanceAccountTotals");

            migrationBuilder.DropTable(
                name: "RemittanceItems");

            migrationBuilder.DropTable(
                name: "RemittanceModeTotals");

            migrationBuilder.DropTable(
                name: "Remittances");

            migrationBuilder.DropIndex(
                name: "IX_Payments_RemittanceId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RemittanceId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Payments");
        }
    }
}
