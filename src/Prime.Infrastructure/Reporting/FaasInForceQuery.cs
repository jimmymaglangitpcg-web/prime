using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Reports;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Reporting;

/// <summary>
/// <see cref="IFaasInForceQuery"/> in SQL (docs/analysis/reporting.md §9, R1). Written by hand: the LINQ form became a
/// correlated sub-query per TD, 104 s for a province-wide summary of 400,000 units. Here each step is one pass over its
/// table, joined by hash, and a summary is one statement (GROUPING SETS give the groups and the total, with each property
/// counted once). The scope (jurisdiction, municipality, barangay, properties) is applied inside every sub-query, so a
/// municipality or a page of properties reads only its own rows. Raw SQL is not seen by EF's query filters, so the
/// jurisdiction is applied here. Only parameter placeholders are added to the text; every value is a parameter.
/// </summary>
public sealed class FaasInForceQuery(PrimeDbContext db, IClock clock, IJurisdiction jurisdiction) : IFaasInForceQuery
{
    /// <summary>Area units (lower case, trimmed) read as square metres and as hectares.</summary>
    private const string SquareMetres = "'sqm', 'sq m', 'sq.m.', 'm2'";
    private const string Hectares = "'ha', 'hectare', 'hectares'";

    /// <summary>The sums of a summary row over the FAAS values <c>f</c>.</summary>
    private const string Sums = """
        COUNT(DISTINCT f."PropertyId")::int AS "Properties",
        COUNT(*)::int AS "Units",
        COALESCE(SUM(f."LandAreaSqm"), 0) AS "LandAreaSqm",
        (COUNT(*) FILTER (WHERE f."LandAreaUnconverted"))::int AS "UnconvertedLandUnits",
        COALESCE(SUM(f."TaxableMarketValue"), 0) AS "TaxableMarketValue",
        COALESCE(SUM(f."TaxableAssessedValue"), 0) AS "TaxableAssessedValue",
        COALESCE(SUM(f."ExemptMarketValue"), 0) AS "ExemptMarketValue",
        COALESCE(SUM(f."ExemptAssessedValue"), 0) AS "ExemptAssessedValue"
        """;

    /// <summary>Sort and hash memory for one report read (PostgreSQL's default is 4 MB; see <see cref="TunedAsync{T}"/>).</summary>
    private const string WorkMemory = "64MB";

    public async Task<IReadOnlyList<FaasValue>> ListAsync(FaasScope scope, CancellationToken cancellationToken)
    {
        var (sql, parameters) = Values(scope);
        return await TunedAsync(() => db.Database.SqlQueryRaw<FaasValue>(sql, parameters).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<FaasGroup>> SummaryAsync(FaasScope scope, IReadOnlyCollection<FaasGroupBy> groupings, CancellationToken cancellationToken)
    {
        var (values, parameters) = Values(scope);
        var keys = groupings.Where(g => g != FaasGroupBy.None).Distinct().Select(g => (GroupBy: g, Column: g switch
        {
            FaasGroupBy.Barangay => "f.\"BarangayId\"",
            FaasGroupBy.Classification => "f.\"ClassificationId\"",
            FaasGroupBy.ActualUse => "f.\"ActualUseId\"",
            _ => "f.\"ZoneId\"",
        })).ToList();
        // GROUPING(k) is 0 on the rows of k's set; the total's row has every key rolled up.
        var head = keys.Count == 0
            ? "0 AS \"GroupBy\", NULL::uuid AS \"Key\", TRUE AS \"IsTotal\""
            : $"CASE {string.Concat(keys.Select(k => $"WHEN GROUPING({k.Column}) = 0 THEN {(int)k.GroupBy} "))}ELSE 0 END AS \"GroupBy\", "
              + $"CASE {string.Concat(keys.Select(k => $"WHEN GROUPING({k.Column}) = 0 THEN {k.Column} "))}END AS \"Key\", "
              + $"({string.Join(" AND ", keys.Select(k => $"GROUPING({k.Column}) = 1"))}) AS \"IsTotal\"";
        var sql = $$"""
            SELECT {{head}}, NULL::text AS "Kind",
                   {{Sums}}
            FROM ({{values}}) f
            {{(keys.Count == 0 ? string.Empty : $"GROUP BY GROUPING SETS ({string.Concat(keys.Select(k => $"({k.Column}), "))}())")}}
            """;
        return await TunedAsync(() => db.Database.SqlQueryRaw<FaasGroup>(sql, parameters).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<FaasGroup>> KindSummaryAsync(FaasScope scope, CancellationToken cancellationToken)
    {
        var (values, parameters) = Values(scope);
        // GROUPING(classification) is 1 on a kind's subtotal; GROUPING(kind) is 1 only on the total.
        var sql = $$"""
            SELECT 0 AS "GroupBy", f."ClassificationId" AS "Key", f."RpuType" AS "Kind", (GROUPING(f."RpuType") = 1) AS "IsTotal",
                   {{Sums}}
            FROM ({{values}}) f
            GROUP BY GROUPING SETS ((f."RpuType", f."ClassificationId"), (f."RpuType"), ())
            """;
        return await TunedAsync(() => db.Database.SqlQueryRaw<FaasGroup>(sql, parameters).ToListAsync(cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Runs a report read with settings for one large aggregate, in a transaction of its own so <c>SET LOCAL</c> ends with
    /// it (inside a caller's transaction, they last until that one ends). Measured on 400,000 units: the planner overestimates
    /// the joined rows and JIT-compiles the query, which then takes 160 s instead of 8 s; with more sort and hash memory than
    /// the 4 MB default, 4.4 s (reporting.md §9, R1).
    /// </summary>
    private async Task<T> TunedAsync<T>(Func<Task<T>> read, CancellationToken cancellationToken)
    {
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        await db.Database.ExecuteSqlRawAsync($"SET LOCAL jit = off; SET LOCAL work_mem = '{WorkMemory}'", cancellationToken);
        var result = await read();
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return result;
    }

    /// <summary>The SQL of one row per FAAS in force in the scope, and its parameters ({0} the date, {1} the next day's start).</summary>
    private (string Sql, object[] Parameters) Values(FaasScope scope)
    {
        var parameters = new List<object> { scope.AsOf, clock.StartOfDay(scope.AsOf.AddDays(1)) };
        string Parameter(object value)
        {
            parameters.Add(value);
            return $"{{{parameters.Count - 1}}}";
        }

        var conditions = new List<string>();
        if (jurisdiction.Restricted)
        {
            conditions.Add($"p.\"MunicipalityId\" = ANY({Parameter(jurisdiction.MunicipalityIds.ToArray())})");
        }
        if (scope.MunicipalityId is { } m)
        {
            conditions.Add($"p.\"MunicipalityId\" = {Parameter(m)}");
        }
        if (scope.BarangayId is { } b)
        {
            conditions.Add($"p.\"BarangayId\" = {Parameter(b)}");
        }
        if (scope.PropertyIds is { } ids)
        {
            conditions.Add($"p.\"Id\" = ANY({Parameter(ids.ToArray())})");
        }
        // Each sub-query reads only the scope's rows; unscoped, the joins to Property are left out.
        var where = conditions.Count == 0 ? string.Empty : "AND " + string.Join(" AND ", conditions);
        var postedJoin = conditions.Count == 0 ? string.Empty : "JOIN \"Property\" p ON p.\"Id\" = a.\"PropertyId\"";
        var linesJoin = conditions.Count == 0 ? string.Empty
            : "JOIN \"Assessments\" a ON a.\"Id\" = l.\"AssessmentId\" JOIN \"Property\" p ON p.\"Id\" = a.\"PropertyId\"";

        var sql = $$"""
            SELECT td."Id" AS "TaxDeclarationId", td."TaxDeclarationNumber", td."PropertyId", td."RpuId",
                   land."Id" IS NOT NULL AS "IsLand", rpu."RpuType",
                   td."MunicipalityId", td."BarangayId", td."ZoneId", td."ClassificationId", td."ActualUseId",
                   CASE WHEN ln."AssessmentId" IS NOT NULL THEN COALESCE(ln.tmv, 0)
                        WHEN td."Taxability" = 'Taxable' THEN COALESCE(da."MarketValue", pa."MarketValue", 0) ELSE 0 END AS "TaxableMarketValue",
                   CASE WHEN ln."AssessmentId" IS NOT NULL THEN COALESCE(ln.tav, 0)
                        WHEN td."Taxability" = 'Taxable' THEN COALESCE(da."AssessedValue", pa."AssessedValue", 0) ELSE 0 END AS "TaxableAssessedValue",
                   CASE WHEN ln."AssessmentId" IS NOT NULL THEN COALESCE(ln.emv, 0)
                        WHEN td."Taxability" = 'Taxable' THEN 0 ELSE COALESCE(da."MarketValue", pa."MarketValue", 0) END AS "ExemptMarketValue",
                   CASE WHEN ln."AssessmentId" IS NOT NULL THEN COALESCE(ln.eav, 0)
                        WHEN td."Taxability" = 'Taxable' THEN 0 ELSE COALESCE(da."AssessedValue", pa."AssessedValue", 0) END AS "ExemptAssessedValue",
                   CASE WHEN LOWER(TRIM(land."AreaUnit")) IN ({{SquareMetres}}) THEN land."Area"
                        WHEN LOWER(TRIM(land."AreaUnit")) IN ({{Hectares}}) THEN land."Area" * 10000 END AS "LandAreaSqm",
                   COALESCE(LOWER(TRIM(land."AreaUnit")) NOT IN ({{SquareMetres}}, {{Hectares}}), FALSE) AS "LandAreaUnconverted"
            FROM (
                SELECT DISTINCT ON (t."RpuId") t."Id", t."TaxDeclarationNumber", t."PropertyId", t."RpuId", t."AssessmentId", t."Taxability",
                       t."ClassificationId", t."ActualUseId", p."MunicipalityId", p."BarangayId", p."ZoneId"
                FROM "TaxDeclarations" t
                JOIN "Property" p ON p."Id" = t."PropertyId"
                WHERE t."EffectivityDate" <= {0}
                  AND (t."Status" = 'Approved' OR (t."Status" = 'Cancelled' AND t."CancelledAt" >= {1}))
                  AND (t."ApprovedAt" IS NULL OR t."ApprovedAt" < {1})
                  {{where}}
                ORDER BY t."RpuId", t."EffectivityDate" DESC, t."RevisionNumber" DESC, t."CreatedAt" DESC
            ) td
            LEFT JOIN "Assessments" da ON da."Id" = td."AssessmentId"
            LEFT JOIN (
                SELECT DISTINCT ON (a."RpuId") a."RpuId", a."MarketValue", a."AssessedValue"
                FROM "Assessments" a {{postedJoin}}
                WHERE a."Status" = 'Posted' AND a."EffectiveDate" <= {0} {{where}}
                ORDER BY a."RpuId", a."EffectiveDate" DESC, a."CreatedAt" DESC
            ) pa ON td."AssessmentId" IS NULL AND pa."RpuId" = td."RpuId"
            LEFT JOIN (
                SELECT l."AssessmentId",
                       SUM(l."MarketValue") FILTER (WHERE l."Taxability" = 'Taxable') AS tmv,
                       SUM(l."AssessedValue") FILTER (WHERE l."Taxability" = 'Taxable') AS tav,
                       SUM(l."MarketValue") FILTER (WHERE l."Taxability" = 'Exempt') AS emv,
                       SUM(l."AssessedValue") FILTER (WHERE l."Taxability" = 'Exempt') AS eav
                FROM "AssessmentLines" l {{linesJoin}}
                WHERE TRUE {{where}}
                GROUP BY l."AssessmentId"
            ) ln ON ln."AssessmentId" = td."AssessmentId"
            LEFT JOIN "Lands" land ON land."RpuId" = td."RpuId"
            JOIN "RealPropertyUnit" rpu ON rpu."Id" = td."RpuId"
            """;
        return (sql, [.. parameters]);
    }
}
