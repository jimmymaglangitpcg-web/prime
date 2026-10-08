using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditCompleteness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentRecordId",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentTableName",
                table: "AuditLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ParentRecordId",
                table: "AuditLogs",
                column: "ParentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "UserId", "Timestamp" });

            // The audit log is append-only (CLAUDE.md §48; docs/analysis/workflow-security.md §4.3, Q12): every role,
            // the application's included, is refused UPDATE, DELETE and TRUNCATE. Only a database owner who drops or
            // disables the trigger can change a row, and that is itself outside PRIME.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prime_audit_logs_append_only() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'AuditLogs is append-only: % is not allowed', TG_OP
                        USING ERRCODE = 'insufficient_privilege';
                END;
                $$;

                CREATE TRIGGER audit_logs_append_only_row
                    BEFORE UPDATE OR DELETE ON "AuditLogs"
                    FOR EACH ROW EXECUTE FUNCTION prime_audit_logs_append_only();

                CREATE TRIGGER audit_logs_append_only_truncate
                    BEFORE TRUNCATE ON "AuditLogs"
                    FOR EACH STATEMENT EXECUTE FUNCTION prime_audit_logs_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS audit_logs_append_only_truncate ON "AuditLogs";
                DROP TRIGGER IF EXISTS audit_logs_append_only_row ON "AuditLogs";
                DROP FUNCTION IF EXISTS prime_audit_logs_append_only();
                """);

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ParentRecordId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserId_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ParentRecordId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ParentTableName",
                table: "AuditLogs");
        }
    }
}
