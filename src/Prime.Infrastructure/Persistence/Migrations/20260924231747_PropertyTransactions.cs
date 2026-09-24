using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PropertyTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PropertyTransactionId",
                table: "TaxDeclarations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EndedByTransactionId",
                table: "PropertyTaxpayers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StartedByTransactionId",
                table: "PropertyTaxpayers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TransactionTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionTypes", x => x.Id);
                    table.CheckConstraint("CK_TransactionTypes_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_TransactionTypes_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
                });

            migrationBuilder.CreateTable(
                name: "PropertyTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TypeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TransactionNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyTransactions", x => x.Id);
                    table.CheckConstraint("CK_PropertyTransactions_Approved", "(\"Status\" = 'Approved') = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_PropertyTransactions_Closed", "(\"Status\" IN ('Rejected', 'Cancelled')) = (\"ClosedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PropertyTransactions_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyTransactions_TransactionTypes_TransactionTypeId",
                        column: x => x.TransactionTypeId,
                        principalTable: "TransactionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransactionTypeRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionTypeRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionTypeRequirements_TransactionTypes_TransactionTyp~",
                        column: x => x.TransactionTypeId,
                        principalTable: "TransactionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PropertyTransactionParties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TaxpayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnershipTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnershipPercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyTransactionParties", x => x.Id);
                    table.CheckConstraint("CK_PropertyTransactionParties_OwnershipType", "(\"Role\" = 'Owner') = (\"OwnershipTypeId\" IS NOT NULL)");
                    table.CheckConstraint("CK_PropertyTransactionParties_UnknownOwner", "(\"Role\" = 'UnknownOwner') = (\"TaxpayerId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_PropertyTransactionParties_OwnershipTypes_OwnershipTypeId",
                        column: x => x.OwnershipTypeId,
                        principalTable: "OwnershipTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyTransactionParties_PropertyTransactions_PropertyTra~",
                        column: x => x.PropertyTransactionId,
                        principalTable: "PropertyTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyTransactionParties_Taxpayers_TaxpayerId",
                        column: x => x.TaxpayerId,
                        principalTable: "Taxpayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PropertyTransactionProperties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyTransactionProperties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyTransactionProperties_PropertyTransactions_Property~",
                        column: x => x.PropertyTransactionId,
                        principalTable: "PropertyTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyTransactionProperties_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PropertyTransactionRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SatisfiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SatisfiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyTransactionRequirements", x => x.Id);
                    table.CheckConstraint("CK_PropertyTransactionRequirements_Evidence", "\"SatisfiedAt\" IS NULL OR \"EvidenceReference\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_PropertyTransactionRequirements_PropertyTransactions_Proper~",
                        column: x => x.PropertyTransactionId,
                        principalTable: "PropertyTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PropertyTransactionTdCancellations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxDeclarationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyTransactionTdCancellations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyTransactionTdCancellations_PropertyTransactions_Pro~",
                        column: x => x.PropertyTransactionId,
                        principalTable: "PropertyTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyTransactionTdCancellations_TaxDeclarations_TaxDecla~",
                        column: x => x.TaxDeclarationId,
                        principalTable: "TaxDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_PropertyTransactionId",
                table: "TaxDeclarations",
                column: "PropertyTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTaxpayers_EndedByTransactionId",
                table: "PropertyTaxpayers",
                column: "EndedByTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTaxpayers_StartedByTransactionId",
                table: "PropertyTaxpayers",
                column: "StartedByTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactionParties_OwnershipTypeId",
                table: "PropertyTransactionParties",
                column: "OwnershipTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactionParties_PropertyTransactionId",
                table: "PropertyTransactionParties",
                column: "PropertyTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactionParties_TaxpayerId",
                table: "PropertyTransactionParties",
                column: "TaxpayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactionProperties_PropertyId",
                table: "PropertyTransactionProperties",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactionProperties_PropertyTransactionId_Propert~",
                table: "PropertyTransactionProperties",
                columns: new[] { "PropertyTransactionId", "PropertyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactionRequirements_PropertyTransactionId_Seque~",
                table: "PropertyTransactionRequirements",
                columns: new[] { "PropertyTransactionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactions_PropertyId_EffectiveDate",
                table: "PropertyTransactions",
                columns: new[] { "PropertyId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactions_Status",
                table: "PropertyTransactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactions_TransactionNumber",
                table: "PropertyTransactions",
                column: "TransactionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactions_TransactionTypeId",
                table: "PropertyTransactions",
                column: "TransactionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactionTdCancellations_PropertyTransactionId_Ta~",
                table: "PropertyTransactionTdCancellations",
                columns: new[] { "PropertyTransactionId", "TaxDeclarationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactionTdCancellations_TaxDeclarationId",
                table: "PropertyTransactionTdCancellations",
                column: "TaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionTypeRequirements_TransactionTypeId_Sequence",
                table: "TransactionTypeRequirements",
                columns: new[] { "TransactionTypeId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionTypes_Status_EffectiveDate",
                table: "TransactionTypes",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UX_TransactionTypes_OpenApproved",
                table: "TransactionTypes",
                column: "Code",
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyTaxpayers_PropertyTransactions_EndedByTransactionId",
                table: "PropertyTaxpayers",
                column: "EndedByTransactionId",
                principalTable: "PropertyTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyTaxpayers_PropertyTransactions_StartedByTransaction~",
                table: "PropertyTaxpayers",
                column: "StartedByTransactionId",
                principalTable: "PropertyTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaxDeclarations_PropertyTransactions_PropertyTransactionId",
                table: "TaxDeclarations",
                column: "PropertyTransactionId",
                principalTable: "PropertyTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PropertyTaxpayers_PropertyTransactions_EndedByTransactionId",
                table: "PropertyTaxpayers");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyTaxpayers_PropertyTransactions_StartedByTransaction~",
                table: "PropertyTaxpayers");

            migrationBuilder.DropForeignKey(
                name: "FK_TaxDeclarations_PropertyTransactions_PropertyTransactionId",
                table: "TaxDeclarations");

            migrationBuilder.DropTable(
                name: "PropertyTransactionParties");

            migrationBuilder.DropTable(
                name: "PropertyTransactionProperties");

            migrationBuilder.DropTable(
                name: "PropertyTransactionRequirements");

            migrationBuilder.DropTable(
                name: "PropertyTransactionTdCancellations");

            migrationBuilder.DropTable(
                name: "TransactionTypeRequirements");

            migrationBuilder.DropTable(
                name: "PropertyTransactions");

            migrationBuilder.DropTable(
                name: "TransactionTypes");

            migrationBuilder.DropIndex(
                name: "IX_TaxDeclarations_PropertyTransactionId",
                table: "TaxDeclarations");

            migrationBuilder.DropIndex(
                name: "IX_PropertyTaxpayers_EndedByTransactionId",
                table: "PropertyTaxpayers");

            migrationBuilder.DropIndex(
                name: "IX_PropertyTaxpayers_StartedByTransactionId",
                table: "PropertyTaxpayers");

            migrationBuilder.DropColumn(
                name: "PropertyTransactionId",
                table: "TaxDeclarations");

            migrationBuilder.DropColumn(
                name: "EndedByTransactionId",
                table: "PropertyTaxpayers");

            migrationBuilder.DropColumn(
                name: "StartedByTransactionId",
                table: "PropertyTaxpayers");
        }
    }
}
