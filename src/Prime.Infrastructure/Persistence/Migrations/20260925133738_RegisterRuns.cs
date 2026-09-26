using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegisterRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegisterRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaxpayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AsOf = table.Column<DateOnly>(type: "date", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisterRuns", x => x.Id);
                    table.CheckConstraint("CK_RegisterRuns_Period", "\"FromDate\" IS NULL OR \"FromDate\" <= \"AsOf\"");
                    table.CheckConstraint("CK_RegisterRuns_Scope", "(\"Kind\" = 'OwnershipRecordCard' AND \"TaxpayerId\" IS NOT NULL) OR (\"Kind\" <> 'OwnershipRecordCard' AND \"BarangayId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RegisterRuns_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegisterRuns_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegisterRuns_Taxpayers_TaxpayerId",
                        column: x => x.TaxpayerId,
                        principalTable: "Taxpayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegisterRuns_BarangayId",
                table: "RegisterRuns",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_RegisterRuns_ClassificationId",
                table: "RegisterRuns",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_RegisterRuns_Kind_CreatedAt",
                table: "RegisterRuns",
                columns: new[] { "Kind", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RegisterRuns_TaxpayerId",
                table: "RegisterRuns",
                column: "TaxpayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegisterRuns");
        }
    }
}
