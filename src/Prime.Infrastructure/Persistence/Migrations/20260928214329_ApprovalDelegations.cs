using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalDelegations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalDelegations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfficeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegatingOfficialName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DelegatingOfficialPosition = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InstrumentReference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    InstrumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SubjectTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    PropertyKinds = table.Column<string[]>(type: "text[]", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RenewsDelegationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDelegations", x => x.Id);
                    table.CheckConstraint("CK_ApprovalDelegations_Approval", "(\"Status\" = 'Approved') = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_ApprovalDelegations_Period", "\"ValidTo\" >= \"ValidFrom\"");
                    table.CheckConstraint("CK_ApprovalDelegations_Revocation", "(\"RevokedFrom\" IS NULL) = (\"RevokedAt\" IS NULL) AND (\"RevokedFrom\" IS NULL OR \"Status\" = 'Approved')");
                    table.CheckConstraint("CK_ApprovalDelegations_Subjects", "cardinality(\"SubjectTypes\") > 0");
                    table.ForeignKey(
                        name: "FK_ApprovalDelegations_ApprovalDelegations_RenewsDelegationId",
                        column: x => x.RenewsDelegationId,
                        principalTable: "ApprovalDelegations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalDelegations_Offices_OfficeId",
                        column: x => x.OfficeId,
                        principalTable: "Offices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegations_OfficeId_Status_ValidFrom",
                table: "ApprovalDelegations",
                columns: new[] { "OfficeId", "Status", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegations_RenewsDelegationId",
                table: "ApprovalDelegations",
                column: "RenewsDelegationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalDelegations");
        }
    }
}
