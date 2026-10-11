using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Prime.Domain.Entities.Registers;
using Prime.Domain.Entities.SwornStatements;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

// Table names in raw SQL come only from the constant lists in this class.
#pragma warning disable EF1002

namespace Prime.IntegrationTests.Persistence;

/// <summary>
/// History tables refuse DELETE and TRUNCATE at the database (migration HistoryDeleteGuards; CLAUDE.md §49, §76;
/// docs/analysis/production-hardening.md §4.3). Every statement runs in a transaction that is rolled back.
/// </summary>
public class HistoryDeleteGuardTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] Guarded =
    [
        "Property", "RealPropertyUnit", "Parcels", "Taxpayers", "PropertyTaxpayers", "PinAssignments",
        "Lands", "Buildings", "MachineryUnits",
        "TaxDeclarations", "TaxDeclarationAnnotations", "TaxDeclarationCancellationRequests",
        "Valuations", "ValuationLines", "Assessments", "AssessmentLines",
        "IndependentAppraisals", "IndependentAppraisalInputs", "BackTaxRuns", "BackTaxPeriods",
        "PropertyTransactions", "PropertyTransactionParties", "PropertyTransactionProperties",
        "PropertyTransactionRequirements", "PropertyTransactionTdCancellations",
        "NoticesOfAssessment", "NoticeOfAssessmentItems", "NoticesOfCancellation", "DiscoverySummonses", "SwornStatements",
        "IssuedForms", "AssessmentRollEntries", "AssessmentRollSubmissions", "AssessmentRollSubmissionItems",
        "PropertyExemptions", "ExemptionEvidence",
        "Smvs", "SmvSchedules", "SmvCoverages", "SmvBuildingCosts", "SmvExtraItemCosts",
        "SmvDepreciationSchedules", "SmvDepreciationRows", "AdjustmentFactors", "AdjustmentFactorRows",
        "AssessmentLevels", "AssessmentLevelCeilings", "LevyRates", "SmvPreparations", "SmvPreparationEvents", "SmvConsultations",
        "GeneralRevisionProgrammes", "GeneralRevisionJobs", "GeneralRevisionItems", "GeneralRevisionRunIssues",
        "TerritorialChangeJobs", "TerritorialChangeItems", "TerritorialChangeMappings",
        "ApprovalRecords", "ApprovalDelegations", "RolePermissionChanges", "SignUpRequests", "UserStatusChanges",
        "AppUsers", "ContentImports", "ContentImportItems",
        "TaxBills", "TaxBillTaxTypes", "TaxBillTaxTypeLines", "TaxBillDetails",
        "Payments", "PaymentTenders", "PaymentAllocations", "PaymentCancellations",
        "Remittances", "RemittanceItems", "RemittanceModeTotals", "RemittanceAccountTotals",
    ];

    // A designed delete exists: a register run until it is issued, an item while its statement is a draft.
    private static readonly string[] Conditional = ["RegisterRuns", "SwornStatementItems"];

    // Reference, configuration and working data (drafts, studies, analyses), edited or replaced in place.
    // A new table goes in one of these lists deliberately; the classification test fails until it does.
    private static readonly string[] Unguarded =
    [
        "ActualUses", "AnnotationTypes", "ApprovalChainSteps", "ApprovalChains", "BarangayBoundaries", "Barangays",
        "BuildingComponentTypes", "BuildingComponents", "BuildingFloors", "BuildingMaterials", "BuildingPermitAbstracts",
        "BuildingTypes", "BuildingUsePortions", "CityDistricts", "Classifications", "Conditions", "ConveyanceModes",
        "DiscountRules", "DisputedAreas", "DocumentTypes", "Documents", "ExchangeRates", "ExemptionTypes",
        "FormDefinitions", "GeneralRevisionChecklistStepDefinitions", "GeneralRevisionChecklistSteps",
        "GeneralRevisionScopes", "GeneralRevisionSuspensions", "ImprovementKinds", "InterestRules", "LandAdjustments",
        "LandImprovements", "LandStrips", "MachineryCostItems", "MachineryRegistrationAbstracts", "MachineryTypes",
        "MarketDataReportRuns", "MarketTransactions", "Municipalities", "NumberSequences", "NumberingSchemes",
        "OfficeAssignmentRoles", "OfficeAssignments", "OfficeJurisdictions", "Offices", "OwnershipTypes", "PaymentModes",
        "PaymentScheduleInstallments", "PaymentSchedules", "PenaltyRules", "Permissions", "PriceIndices",
        "PropertyBarangayParts", "PropertyTypes", "Provinces", "RevenueAccountMappings", "RevenueImpactOptionLevels",
        "RevenueImpactOptions", "RevenueImpactRates", "RevenueImpactStudies", "RoadSegments", "RoadTypes",
        "RolePermissions", "Roles", "SalesAnalyses", "SalesAnalysisGroups", "SalesAnalysisSales", "SalesAnalysisScopes",
        "SectionBoundaries", "SmvSimulationResultLines", "SmvSimulationResults", "SmvSimulationRuns",
        "SmvSimulationScopes", "SmvSubClassCriteria", "SmvTimeAdjustmentFactors", "StructuralMaterials",
        "StructuralParts", "StructuralTypes", "SubClassifications", "SubMarketAreas", "TaxIncreaseCapRules",
        "TaxMapSections", "TaxRates", "TaxTypes", "TitleTypes", "TransactionTypeRequirements", "TransactionTypes",
        "TransferTaxClearances", "UserRoles", "ValuationTestRuns", "ValuationTestSales", "ValuationTestScopes",
        "ZoneBoundaries", "Zones",
    ];

    [Fact]
    public async Task Every_table_is_classified_as_guarded_conditional_or_unguarded()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var tables = await db.Database.SqlQueryRaw<string>("""
            SELECT table_name AS "Value" FROM information_schema.tables
            WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
              AND table_name NOT IN ('__EFMigrationsHistory', 'spatial_ref_sys', 'AuditLogs')
            """).ToListAsync();
        var classified = Guarded.Concat(Conditional).Concat(Unguarded).ToList();
        classified.ShouldBeUnique();
        tables.Except(classified).ShouldBeEmpty("a new table must be classified in HistoryDeleteGuardTests (and guarded by a migration if it holds history)");
        classified.Except(tables).ShouldBeEmpty("a classified table no longer exists");
    }

    [Fact]
    public async Task Every_history_table_carries_the_delete_and_truncate_triggers()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var triggers = await db.Database.SqlQueryRaw<string>("""
            SELECT c.relname || ':' || t.tgname AS "Value" FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
            WHERE t.tgname IN ('history_no_delete_row', 'history_no_truncate') AND t.tgenabled = 'O'
            """).ToListAsync();
        foreach (var table in Guarded.Concat(Conditional))
        {
            triggers.ShouldContain($"{table}:history_no_delete_row");
            triggers.ShouldContain($"{table}:history_no_truncate");
        }
    }

    [Fact]
    public async Task Truncating_any_history_table_is_refused()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        foreach (var table in Guarded.Concat(Conditional))
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            // CASCADE: a table referenced by a foreign key would otherwise fail on the reference, not the guard.
            var error = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync($"TRUNCATE \"{table}\" CASCADE"));
            error.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege, table);
        }
    }

    [Fact]
    public async Task Deleting_a_row_of_any_history_table_that_has_rows_is_refused()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var tried = 0;
        foreach (var table in Guarded)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var sql = $"""DELETE FROM "{table}" WHERE ctid = (SELECT ctid FROM "{table}" LIMIT 1)""";
            if (!await db.Database.SqlQueryRaw<bool>($"""SELECT EXISTS (SELECT 1 FROM "{table}") AS "Value" """).SingleAsync())
            {
                continue;
            }
            var error = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));
            error.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege, table);
            error.MessageText.ShouldContain($"{table} is a history table");
            tried++;
        }
        // The DEMO users created on start-up alone make AppUsers non-empty; the triggers' presence is proven above.
        tried.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task A_register_run_can_be_deleted_until_a_form_is_issued_from_it()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var barangayId = await db.Barangays.Select(x => x.Id).FirstAsync();
        RegisterRun Run() => new() { Kind = RegisterKind.AssessmentRollTaxable, BarangayId = barangayId, AsOf = new DateOnly(2026, 1, 31), Remarks = "DEMO guard test" };
        var unissued = Run();
        var issued = Run();
        db.RegisterRuns.AddRange(unissued, issued);
        await db.SaveChangesAsync();
        var definitionId = await db.FormDefinitions.Select(x => x.Id).FirstAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "IssuedForms" ("Id", "FormDefinitionId", "FormCode", "FormVersion", "Authority", "SubjectType", "SubjectId",
                "DataSnapshotJson", "RenderedHtml", "RenderedHtmlSha256", "Status", "IssuedAt", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {definitionId}, 'DEMO', 1, 'Provisional', 'Register', {issued.Id},
                {"{}"}::jsonb, '', {new string('0', 64)}, 'Posted', now(), now())
            """);

        (await db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "RegisterRuns" WHERE "Id" = {unissued.Id}""")).ShouldBe(1);
        var error = await Should.ThrowAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "RegisterRuns" WHERE "Id" = {issued.Id}"""));
        error.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task A_sworn_statement_item_can_be_deleted_only_while_the_statement_is_a_draft()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var municipalityId = await db.Municipalities.Select(x => x.Id).FirstAsync();
        var day = new DateOnly(2026, 1, 15);
        SwornStatement Statement(SwornStatementStatus status) => new()
        {
            DeclarantName = "DEMO Declarant", MunicipalityId = municipalityId, Status = status,
            SignedOn = day, SwornOn = day, ReceivedOn = day, AdministeringOfficer = "DEMO Officer",
            FiledAt = status == SwornStatementStatus.Filed ? DateTimeOffset.UtcNow : null,
            Items = [new SwornStatementItem { Kind = SwornStatementItemKind.Machinery, Description = "DEMO machine", Sequence = 1, DeclaredMarketValue = 1m }],
        };
        var draft = Statement(SwornStatementStatus.Draft);
        var filed = Statement(SwornStatementStatus.Filed);
        db.SwornStatements.AddRange(draft, filed);
        await db.SaveChangesAsync();

        (await db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "SwornStatementItems" WHERE "Id" = {draft.Items[0].Id}""")).ShouldBe(1);
        var error = await Should.ThrowAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "SwornStatementItems" WHERE "Id" = {filed.Items[0].Id}"""));
        error.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }
}
