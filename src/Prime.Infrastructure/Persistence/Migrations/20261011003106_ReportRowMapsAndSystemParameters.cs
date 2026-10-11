using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportRowMapsAndSystemParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportRowMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Definition = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_ReportRowMaps", x => x.Id);
                    table.CheckConstraint("CK_ReportRowMaps_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_ReportRowMaps_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
                });

            migrationBuilder.CreateTable(
                name: "SystemParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_SystemParameters", x => x.Id);
                    table.CheckConstraint("CK_SystemParameters_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_SystemParameters_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportRowMaps_Code_EffectiveDate",
                table: "ReportRowMaps",
                columns: new[] { "Code", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportRowMaps_Status_EffectiveDate",
                table: "ReportRowMaps",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UX_ReportRowMaps_OpenApproved",
                table: "ReportRowMaps",
                column: "Code",
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SystemParameters_Code_EffectiveDate",
                table: "SystemParameters",
                columns: new[] { "Code", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemParameters_Status_EffectiveDate",
                table: "SystemParameters",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UX_SystemParameters_OpenApproved",
                table: "SystemParameters",
                column: "Code",
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL");

            // Approved configuration is history (CLAUDE.md §49): refused DELETE and TRUNCATE, as HistoryDeleteGuards does.
            migrationBuilder.Sql("""
                CREATE TRIGGER history_no_delete_row BEFORE DELETE ON "ReportRowMaps"
                    FOR EACH ROW EXECUTE FUNCTION prime_history_no_delete();
                CREATE TRIGGER history_no_truncate BEFORE TRUNCATE ON "ReportRowMaps"
                    FOR EACH STATEMENT EXECUTE FUNCTION prime_history_no_delete();
                CREATE TRIGGER history_no_delete_row BEFORE DELETE ON "SystemParameters"
                    FOR EACH ROW EXECUTE FUNCTION prime_history_no_delete();
                CREATE TRIGGER history_no_truncate BEFORE TRUNCATE ON "SystemParameters"
                    FOR EACH STATEMENT EXECUTE FUNCTION prime_history_no_delete();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS history_no_truncate ON "ReportRowMaps";
                DROP TRIGGER IF EXISTS history_no_delete_row ON "ReportRowMaps";
                DROP TRIGGER IF EXISTS history_no_truncate ON "SystemParameters";
                DROP TRIGGER IF EXISTS history_no_delete_row ON "SystemParameters";
                """);
            migrationBuilder.DropTable(
                name: "ReportRowMaps");

            migrationBuilder.DropTable(
                name: "SystemParameters");
        }
    }
}
