using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentCancellations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacesPaymentId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentCancellations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsCorrection = table.Column<bool>(type: "boolean", nullable: false),
                    ReplacementRequestJson = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionRemarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TransactionNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReplacementPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentCancellations", x => x.Id);
                    table.CheckConstraint("CK_PaymentCancellations_Correction", "\"IsCorrection\" = (\"ReplacementRequestJson\" IS NOT NULL)");
                    table.CheckConstraint("CK_PaymentCancellations_Decision", "(\"Status\" = 'Pending') = (\"DecidedAt\" IS NULL) AND (\"Status\" = 'Approved') = (\"Kind\" IS NOT NULL AND \"TransactionNumber\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PaymentCancellations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentCancellations_Payments_ReplacementPaymentId",
                        column: x => x.ReplacementPaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReplacesPaymentId",
                table: "Payments",
                column: "ReplacesPaymentId",
                unique: true,
                filter: "\"ReplacesPaymentId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Cancelled",
                table: "Payments",
                sql: "(\"Status\" = 'Posted') = (\"CancelledAt\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentCancellations_ReplacementPaymentId",
                table: "PaymentCancellations",
                column: "ReplacementPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentCancellations_Status",
                table: "PaymentCancellations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentCancellations_TransactionNumber",
                table: "PaymentCancellations",
                column: "TransactionNumber",
                unique: true,
                filter: "\"TransactionNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentCancellations_Pending",
                table: "PaymentCancellations",
                column: "PaymentId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Payments_ReplacesPaymentId",
                table: "Payments",
                column: "ReplacesPaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Payments_ReplacesPaymentId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "PaymentCancellations");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReplacesPaymentId",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Cancelled",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReplacesPaymentId",
                table: "Payments");
        }
    }
}
