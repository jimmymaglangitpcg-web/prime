using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Exemptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExemptionTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AppliesTo = table.Column<int>(type: "integer", nullable: false),
                    RequiresProof = table.Column<bool>(type: "boolean", nullable: false),
                    AssessedValueCeiling = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
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
                    table.PrimaryKey("PK_ExemptionTypes", x => x.Id);
                    table.CheckConstraint("CK_ExemptionTypes_AppliesTo", "\"AppliesTo\" BETWEEN 1 AND 15");
                    table.CheckConstraint("CK_ExemptionTypes_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_ExemptionTypes_Ceiling", "\"AssessedValueCeiling\" IS NULL OR \"AssessedValueCeiling\" > 0");
                    table.CheckConstraint("CK_ExemptionTypes_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
                });

            migrationBuilder.CreateTable(
                name: "PropertyExemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RpuId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExemptionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: true),
                    PortionDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClaimantTaxpayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ProofDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProofFiledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionRemarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EndedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyExemptions", x => x.Id);
                    table.CheckConstraint("CK_PropertyExemptions_Approved", "\"Status\" NOT IN ('Approved', 'Ended') OR \"EffectiveDate\" IS NOT NULL");
                    table.CheckConstraint("CK_PropertyExemptions_Decided", "(\"Status\" IN ('Approved', 'Rejected', 'Ended')) = (\"DecidedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_PropertyExemptions_Ended", "(\"Status\" = 'Ended') = (\"EndedOn\" IS NOT NULL AND \"EndReason\" IS NOT NULL)");
                    table.CheckConstraint("CK_PropertyExemptions_Expiry", "\"ExpiryDate\" IS NULL OR \"ExpiryDate\" >= \"EffectiveDate\"");
                    table.CheckConstraint("CK_PropertyExemptions_ProofDue", "\"ProofDueDate\" >= \"ClaimedOn\"");
                    table.ForeignKey(
                        name: "FK_PropertyExemptions_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyExemptions_ExemptionTypes_ExemptionTypeId",
                        column: x => x.ExemptionTypeId,
                        principalTable: "ExemptionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyExemptions_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyExemptions_RealPropertyUnit_RpuId",
                        column: x => x.RpuId,
                        principalTable: "RealPropertyUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyExemptions_Taxpayers_ClaimantTaxpayerId",
                        column: x => x.ClaimantTaxpayerId,
                        principalTable: "Taxpayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExemptionEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyExemptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ReceivedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExemptionEvidence", x => x.Id);
                    table.CheckConstraint("CK_ExemptionEvidence_Sequence", "\"Sequence\" >= 1");
                    table.ForeignKey(
                        name: "FK_ExemptionEvidence_PropertyExemptions_PropertyExemptionId",
                        column: x => x.PropertyExemptionId,
                        principalTable: "PropertyExemptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExemptionEvidence_PropertyExemptionId_Sequence",
                table: "ExemptionEvidence",
                columns: new[] { "PropertyExemptionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExemptionTypes_Code_EffectiveDate",
                table: "ExemptionTypes",
                columns: new[] { "Code", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ExemptionTypes_Status_EffectiveDate",
                table: "ExemptionTypes",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UX_ExemptionTypes_OpenApproved",
                table: "ExemptionTypes",
                column: "Code",
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyExemptions_ActualUseId",
                table: "PropertyExemptions",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyExemptions_ClaimantTaxpayerId",
                table: "PropertyExemptions",
                column: "ClaimantTaxpayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyExemptions_ExemptionTypeId",
                table: "PropertyExemptions",
                column: "ExemptionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyExemptions_PropertyId",
                table: "PropertyExemptions",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyExemptions_RpuId_Status",
                table: "PropertyExemptions",
                columns: new[] { "RpuId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyExemptions_Status_ProofDueDate",
                table: "PropertyExemptions",
                columns: new[] { "Status", "ProofDueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExemptionEvidence");

            migrationBuilder.DropTable(
                name: "PropertyExemptions");

            migrationBuilder.DropTable(
                name: "ExemptionTypes");
        }
    }
}
