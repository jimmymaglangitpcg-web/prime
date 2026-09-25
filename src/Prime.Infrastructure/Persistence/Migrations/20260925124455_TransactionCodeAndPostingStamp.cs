using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransactionCodeAndPostingStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TransactionCode",
                table: "TaxDeclarations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransactionRank",
                table: "TaxDeclarations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PostedAt",
                table: "Assessments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PostedBy",
                table: "Assessments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_TransactionCode",
                table: "TaxDeclarations",
                column: "TransactionCode");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assessments_Posted",
                table: "Assessments",
                sql: "\"PostedAt\" IS NULL OR \"Status\" = 'Posted'");
            // TDs drafted under a transaction carry its code and its type's rank (docs/analysis/mrpaao-forms-model.md §12).
            migrationBuilder.Sql("""
                UPDATE "TaxDeclarations" td
                SET "TransactionCode" = t."TypeCode", "TransactionRank" = tt."Rank"
                FROM "PropertyTransactions" t
                JOIN "TransactionTypes" tt ON tt."Id" = t."TransactionTypeId"
                WHERE td."PropertyTransactionId" = t."Id" AND td."TransactionCode" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaxDeclarations_TransactionCode",
                table: "TaxDeclarations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assessments_Posted",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "TransactionCode",
                table: "TaxDeclarations");

            migrationBuilder.DropColumn(
                name: "TransactionRank",
                table: "TaxDeclarations");

            migrationBuilder.DropColumn(
                name: "PostedAt",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "PostedBy",
                table: "Assessments");
        }
    }
}
