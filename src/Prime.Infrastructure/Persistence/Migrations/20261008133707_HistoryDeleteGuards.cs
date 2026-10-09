using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// History tables refuse DELETE and TRUNCATE at the database (CLAUDE.md §49, §76; docs/analysis/production-hardening.md
    /// §4.3, Q5), as <c>AuditLogs</c> already does. PRIME never deletes these rows — it cancels, voids, retires or
    /// supersedes them — so the triggers only stop a mistake or a stray script. Two tables allow a designed delete:
    /// a register run that was never issued, and an item on a draft sworn statement.
    /// </summary>
    public partial class HistoryDeleteGuards : Migration
    {
        // The list as of this migration; a later history table gets its trigger in its own migration.
        private static readonly string[] HistoryTables =
        [
            // Property registry and ownership
            "Property", "RealPropertyUnit", "Parcels", "Taxpayers", "PropertyTaxpayers", "PinAssignments",
            "Lands", "Buildings", "MachineryUnits",
            // Tax Declarations
            "TaxDeclarations", "TaxDeclarationAnnotations", "TaxDeclarationCancellationRequests",
            // Appraisal and assessment
            "Valuations", "ValuationLines", "Assessments", "AssessmentLines",
            "IndependentAppraisals", "IndependentAppraisalInputs", "BackTaxRuns", "BackTaxPeriods",
            // Transactions, notices and statements
            "PropertyTransactions", "PropertyTransactionParties", "PropertyTransactionProperties",
            "PropertyTransactionRequirements", "PropertyTransactionTdCancellations",
            "NoticesOfAssessment", "NoticeOfAssessmentItems", "NoticesOfCancellation", "DiscoverySummonses",
            "SwornStatements",
            // Issued forms and registers
            "IssuedForms", "AssessmentRollEntries", "AssessmentRollSubmissions", "AssessmentRollSubmissionItems",
            // Exemptions
            "PropertyExemptions", "ExemptionEvidence",
            // SMVs, assessment levels and adjustment factors
            "Smvs", "SmvSchedules", "SmvCoverages", "SmvBuildingCosts", "SmvExtraItemCosts",
            "SmvDepreciationSchedules", "SmvDepreciationRows", "AdjustmentFactors", "AdjustmentFactorRows",
            "AssessmentLevels", "AssessmentLevelCeilings", "SmvPreparations", "SmvPreparationEvents", "SmvConsultations",
            // General revision and territorial changes
            "GeneralRevisionProgrammes", "GeneralRevisionJobs", "GeneralRevisionItems", "GeneralRevisionRunIssues",
            "TerritorialChangeJobs", "TerritorialChangeItems", "TerritorialChangeMappings",
            // Approvals, users and content
            "ApprovalRecords", "ApprovalDelegations", "RolePermissionChanges", "SignUpRequests", "UserStatusChanges",
            "AppUsers", "ContentImports", "ContentImportItems",
            // Frozen treasury records (CLAUDE.md §0; Q11)
            "TaxBills", "TaxBillTaxTypes", "TaxBillTaxTypeLines", "TaxBillDetails",
            "Payments", "PaymentTenders", "PaymentAllocations", "PaymentCancellations",
            "Remittances", "RemittanceItems", "RemittanceModeTotals", "RemittanceAccountTotals",
        ];

        private static readonly string[] ConditionalTables = ["RegisterRuns", "SwornStatementItems"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prime_history_no_delete() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION '% is a history table: % is not allowed', TG_TABLE_NAME, TG_OP
                        USING ERRCODE = 'insufficient_privilege';
                END;
                $$;

                -- A run is a working record until a form is issued from it; an issued run is history.
                CREATE OR REPLACE FUNCTION prime_register_runs_no_delete_issued() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "IssuedForms" WHERE "SubjectType" = 'Register' AND "SubjectId" = OLD."Id") THEN
                        RAISE EXCEPTION 'RegisterRuns: an issued register run cannot be deleted'
                            USING ERRCODE = 'insufficient_privilege';
                    END IF;
                    RETURN OLD;
                END;
                $$;

                -- A draft statement is not yet a record: its items may be removed until it is filed.
                CREATE OR REPLACE FUNCTION prime_sworn_statement_items_draft_only() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM "SwornStatements" WHERE "Id" = OLD."SwornStatementId" AND "Status" = 'Draft') THEN
                        RAISE EXCEPTION 'SwornStatementItems: an item of a filed statement cannot be deleted'
                            USING ERRCODE = 'insufficient_privilege';
                    END IF;
                    RETURN OLD;
                END;
                $$;
                """);

            foreach (var table in HistoryTables)
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER history_no_delete_row BEFORE DELETE ON "{table}"
                        FOR EACH ROW EXECUTE FUNCTION prime_history_no_delete();
                    """);
            }
            migrationBuilder.Sql("""
                CREATE TRIGGER history_no_delete_row BEFORE DELETE ON "RegisterRuns"
                    FOR EACH ROW EXECUTE FUNCTION prime_register_runs_no_delete_issued();
                CREATE TRIGGER history_no_delete_row BEFORE DELETE ON "SwornStatementItems"
                    FOR EACH ROW EXECUTE FUNCTION prime_sworn_statement_items_draft_only();
                """);
            foreach (var table in HistoryTables.Concat(ConditionalTables))
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER history_no_truncate BEFORE TRUNCATE ON "{table}"
                        FOR EACH STATEMENT EXECUTE FUNCTION prime_history_no_delete();
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in HistoryTables.Concat(ConditionalTables))
            {
                migrationBuilder.Sql($"""
                    DROP TRIGGER IF EXISTS history_no_truncate ON "{table}";
                    DROP TRIGGER IF EXISTS history_no_delete_row ON "{table}";
                    """);
            }
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS prime_sworn_statement_items_draft_only();
                DROP FUNCTION IF EXISTS prime_register_runs_no_delete_issued();
                DROP FUNCTION IF EXISTS prime_history_no_delete();
                """);
        }
    }
}
