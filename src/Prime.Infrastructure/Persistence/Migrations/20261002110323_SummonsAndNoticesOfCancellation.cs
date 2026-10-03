using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SummonsAndNoticesOfCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CancelsMotuProprio",
                table: "TransactionTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "InterAgencyVerification",
                table: "PropertyTransactions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "VerificationRecordedOn",
                table: "PropertyTransactions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BlocksCancellation",
                table: "AnnotationTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DiscoverySummonses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    SummonsNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AddresseeName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AddresseeAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AddresseeTaxpayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodDays = table.Column<int>(type: "integer", nullable: false),
                    ServiceMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ServedTo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ProofReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ServiceNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OutcomeOn = table.Column<DateOnly>(type: "date", nullable: true),
                    OutcomeNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoverySummonses", x => x.Id);
                    table.CheckConstraint("CK_DiscoverySummonses_Outcome", "(\"Outcome\" = 'Pending') = (\"OutcomeOn\" IS NULL)");
                    table.CheckConstraint("CK_DiscoverySummonses_OutcomeServed", "\"Outcome\" = 'Pending' OR \"ReceivedOn\" IS NOT NULL");
                    table.CheckConstraint("CK_DiscoverySummonses_Period", "\"PeriodDays\" > 0");
                    table.CheckConstraint("CK_DiscoverySummonses_Sequence", "\"Sequence\" IN (1, 2)");
                    table.CheckConstraint("CK_DiscoverySummonses_Served", "(\"ReceivedOn\" IS NOT NULL) = (\"ServiceMode\" IS NOT NULL AND \"ProofReference\" IS NOT NULL AND \"DueDate\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_DiscoverySummonses_PropertyTransactions_PropertyTransaction~",
                        column: x => x.PropertyTransactionId,
                        principalTable: "PropertyTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiscoverySummonses_Taxpayers_AddresseeTaxpayerId",
                        column: x => x.AddresseeTaxpayerId,
                        principalTable: "Taxpayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoticesOfCancellation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxDeclarationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxDeclarationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReplacedByTaxDeclarationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReplacedByTaxDeclarationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PropertyTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Ground = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CancelledOn = table.Column<DateOnly>(type: "date", nullable: false),
                    AddresseeNames = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AddresseeAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NoticeNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IssuedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ServedTo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EmailAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    SentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProofReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ServiceNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ServiceRecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ServiceRecordedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticesOfCancellation", x => x.Id);
                    table.CheckConstraint("CK_NoticesOfCancellation_Cancelled", "(\"Status\" = 'Cancelled') = (\"CancelledAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_NoticesOfCancellation_Email", "(\"ServiceMode\" = 'Email') <= (\"EmailAddress\" IS NOT NULL)");
                    table.CheckConstraint("CK_NoticesOfCancellation_Issued", "(\"Status\" IN ('Issued', 'Served')) <= (\"IssuedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_NoticesOfCancellation_Served", "(\"Status\" = 'Served') = (\"ReceivedDate\" IS NOT NULL AND \"ServiceMode\" IS NOT NULL AND \"ProofReference\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_NoticesOfCancellation_PropertyTransactions_PropertyTransact~",
                        column: x => x.PropertyTransactionId,
                        principalTable: "PropertyTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoticesOfCancellation_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoticesOfCancellation_TaxDeclarations_ReplacedByTaxDeclarat~",
                        column: x => x.ReplacedByTaxDeclarationId,
                        principalTable: "TaxDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoticesOfCancellation_TaxDeclarations_TaxDeclarationId",
                        column: x => x.TaxDeclarationId,
                        principalTable: "TaxDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscoverySummonses_AddresseeTaxpayerId",
                table: "DiscoverySummonses",
                column: "AddresseeTaxpayerId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoverySummonses_PropertyTransactionId_Sequence",
                table: "DiscoverySummonses",
                columns: new[] { "PropertyTransactionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoticesOfCancellation_PropertyId_Status",
                table: "NoticesOfCancellation",
                columns: new[] { "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_NoticesOfCancellation_PropertyTransactionId",
                table: "NoticesOfCancellation",
                column: "PropertyTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_NoticesOfCancellation_ReplacedByTaxDeclarationId",
                table: "NoticesOfCancellation",
                column: "ReplacedByTaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_NoticesOfCancellation_TaxDeclarationId",
                table: "NoticesOfCancellation",
                column: "TaxDeclarationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscoverySummonses");

            migrationBuilder.DropTable(
                name: "NoticesOfCancellation");

            migrationBuilder.DropColumn(
                name: "CancelsMotuProprio",
                table: "TransactionTypes");

            migrationBuilder.DropColumn(
                name: "InterAgencyVerification",
                table: "PropertyTransactions");

            migrationBuilder.DropColumn(
                name: "VerificationRecordedOn",
                table: "PropertyTransactions");

            migrationBuilder.DropColumn(
                name: "BlocksCancellation",
                table: "AnnotationTypes");
        }
    }
}
