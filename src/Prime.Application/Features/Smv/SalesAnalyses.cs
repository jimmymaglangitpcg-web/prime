using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Entities.MarketData;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Smv;

public sealed record AddTimeAdjustmentFactorRequest(DateOnly PeriodFrom, DateOnly PeriodTo, decimal Factor, string Source);
public sealed record TimeAdjustmentFactorDto(Guid Id, DateOnly PeriodFrom, DateOnly PeriodTo, decimal Factor, string Source);

public sealed record CreateSalesAnalysisRequest(Guid ClassificationId, Guid? ActualUseId, IReadOnlyList<Guid>? MunicipalityIds, DateOnly? SalesFrom,
    DateOnly? SalesTo, AreaMeasure AreaUnit, decimal? RoundingIncrement, decimal? RangeWidthPercent, string? Notes);
public sealed record UpdateSalesAnalysisRequest(decimal RoundingIncrement, decimal? RangeWidthPercent, string? Notes);
public sealed record UpdateAnalysisSaleRequest(bool LeftOut, string? ExclusionReason, decimal OtherAdjustmentPercent, string? Note);
public sealed record SalesAnalysisGroupInput(decimal FromValue, decimal ToValue, Guid? SubClassificationId, decimal? AdoptedValue, string? Basis);
public sealed record SetSalesAnalysisGroupsRequest(IReadOnlyList<SalesAnalysisGroupInput> Groups);

public sealed record SalesAnalysisSaleDto(
    Guid Id, Guid MarketTransactionId, DateOnly TransactionDate, string? BarangayName, string? Location, string? TaxDeclarationNumber, string? Pin,
    string? SubClassificationName, decimal? Area, decimal? Price, decimal? UnitPrice, decimal? TimeFactor, decimal OtherAdjustmentPercent,
    decimal? AdjustedUnitPrice, decimal? RoundedUnitValue, bool LeftOut, string? ExclusionReason, string? Note);

/// <summary>A row of Table 1: the analysed values from lowest to highest, with each one's interval.</summary>
public sealed record SalesAnalysisValueDto(int Number, Guid SaleId, decimal AdjustedUnitPrice, decimal RoundedUnitValue, decimal? IntervalPercent);
public sealed record SalesAnalysisRangeDto(int Number, decimal Low, decimal Mid, decimal High, int Frequency, Guid? GroupId);
public sealed record SalesAnalysisGroupDto(
    Guid Id, int Sequence, decimal FromValue, decimal ToValue, Guid? SubClassificationId, string? SubClassificationName, int Frequency,
    decimal? ProposedValue, decimal? AdoptedValue, string? Basis, Guid? SmvScheduleId, DateTimeOffset? AdoptedAt);

public sealed record SalesAnalysisDto(
    Guid Id, Guid SmvPreparationId, SmvPreparationStatus PreparationStatus, bool Editable, DateOnly? BaseValuationDate,
    Guid ClassificationId, string ClassificationName, Guid? ActualUseId, string? ActualUseName, IReadOnlyList<string> Municipalities,
    DateOnly? SalesFrom, DateOnly? SalesTo, AreaMeasure AreaUnit, decimal RoundingIncrement, decimal? RangeWidthPercent,
    decimal? AverageIntervalPercent, decimal? EffectiveWidthPercent, string? Notes,
    IReadOnlyList<SalesAnalysisSaleDto> Sales, IReadOnlyList<SalesAnalysisValueDto> Values, IReadOnlyList<SalesAnalysisRangeDto> Ranges,
    IReadOnlyList<SalesAnalysisGroupDto> Groups, IReadOnlyList<string> Warnings);

public sealed record SalesAnalysisSummaryDto(Guid Id, string ClassificationName, string? ActualUseName, AreaMeasure AreaUnit, int SaleCount, int AnalysedCount,
    int GroupCount, int AdoptedCount);

public interface ISalesAnalysisService
{
    Task<Result<IReadOnlyList<TimeAdjustmentFactorDto>>> ListTimeFactorsAsync(Guid preparationId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TimeAdjustmentFactorDto>>> AddTimeFactorAsync(Guid preparationId, AddTimeAdjustmentFactorRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TimeAdjustmentFactorDto>>> RemoveTimeFactorAsync(Guid preparationId, Guid factorId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SalesAnalysisSummaryDto>>> ListAsync(Guid preparationId, CancellationToken cancellationToken = default);
    Task<Result<SalesAnalysisDto>> CreateAsync(Guid preparationId, CreateSalesAnalysisRequest request, CancellationToken cancellationToken = default);
    Task<Result<SalesAnalysisDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<SalesAnalysisDto>> UpdateAsync(Guid id, UpdateSalesAnalysisRequest request, CancellationToken cancellationToken = default);
    /// <summary>Adds the accepted sales recorded since, and leaves out those no longer accepted.</summary>
    Task<Result<SalesAnalysisDto>> RefreshAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<SalesAnalysisDto>> UpdateSaleAsync(Guid id, Guid saleId, UpdateAnalysisSaleRequest request, CancellationToken cancellationToken = default);
    /// <summary>Replaces the groups not yet adopted.</summary>
    Task<Result<SalesAnalysisDto>> SetGroupsAsync(Guid id, SetSalesAnalysisGroupsRequest request, CancellationToken cancellationToken = default);
    /// <summary>Writes the group's adopted unit value as a draft row of the proposed SMV.</summary>
    Task<Result<SalesAnalysisDto>> AdoptGroupAsync(Guid id, Guid groupId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sales analyses of an SMV preparation (SMV Forms 2–4, 6–8; docs/analysis/smv-preparation-general-revision.md §4.2, Q6–Q7):
/// accepted sales adjusted to the base valuation date by the assessor's factors, rounded, sorted, ranged and counted
/// (<see cref="SalesAnalysisMath"/>); the assessor merges ranges into sub-classes and adopts their unit values, which become draft
/// rows of the proposed SMV. Changed only by the provincial office, and only before the SMV is submitted.
/// </summary>
public sealed class SalesAnalysisService(IApplicationDbContext db, IClock clock, ICurrentUserService currentUser, IJurisdiction jurisdiction, ISmvService smvs)
    : ISalesAnalysisService
{
    private static readonly SmvPreparationStatus[] OpenStatuses =
        [SmvPreparationStatus.Preparing, SmvPreparationStatus.PublishedForComment, SmvPreparationStatus.Remanded];

    public async Task<Result<IReadOnlyList<TimeAdjustmentFactorDto>>> ListTimeFactorsAsync(Guid preparationId, CancellationToken cancellationToken = default) =>
        await db.SmvPreparations.AnyAsync(x => x.Id == preparationId, cancellationToken)
            ? Result.Success(await FactorsAsync(preparationId, cancellationToken))
            : Result.Failure<IReadOnlyList<TimeAdjustmentFactorDto>>("SMV_PREPARATION_NOT_FOUND", "No SMV preparation was found with the given id.");

    public async Task<Result<IReadOnlyList<TimeAdjustmentFactorDto>>> AddTimeFactorAsync(Guid preparationId, AddTimeAdjustmentFactorRequest r,
        CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        var source = r.Source?.Trim();
        if (r.PeriodFrom == default || r.PeriodTo < r.PeriodFrom || r.Factor <= 0 || r.Factor > 10 || string.IsNullOrEmpty(source) || source.Length > 300)
        {
            return Result.Failure<IReadOnlyList<TimeAdjustmentFactorDto>>("VALIDATION_FAILED",
                "A period (from on or before to), a factor above 0 and up to 10, and its source (max 300) are required.");
        }
        var open = await OpenPreparationAsync(preparationId, ct);
        if (open is not null)
        {
            return Result.Failure<IReadOnlyList<TimeAdjustmentFactorDto>>(open.Code!, open.Message!);
        }
        if (await db.SmvTimeAdjustmentFactors.AnyAsync(x => x.SmvPreparationId == preparationId && x.PeriodFrom <= r.PeriodTo && x.PeriodTo >= r.PeriodFrom, ct))
        {
            return Result.Failure<IReadOnlyList<TimeAdjustmentFactorDto>>("TIME_FACTOR_OVERLAP", "Another factor already covers part of that period.");
        }
        db.SmvTimeAdjustmentFactors.Add(new SmvTimeAdjustmentFactor
        {
            SmvPreparationId = preparationId, PeriodFrom = r.PeriodFrom, PeriodTo = r.PeriodTo, Factor = r.Factor, Source = source,
        });
        await db.SaveChangesAsync(ct);
        await RecomputePreparationAsync(preparationId, ct);
        return Result.Success(await FactorsAsync(preparationId, ct));
    }

    public async Task<Result<IReadOnlyList<TimeAdjustmentFactorDto>>> RemoveTimeFactorAsync(Guid preparationId, Guid factorId, CancellationToken cancellationToken = default)
    {
        var open = await OpenPreparationAsync(preparationId, cancellationToken);
        if (open is not null)
        {
            return Result.Failure<IReadOnlyList<TimeAdjustmentFactorDto>>(open.Code!, open.Message!);
        }
        var factor = await db.SmvTimeAdjustmentFactors.FirstOrDefaultAsync(x => x.Id == factorId && x.SmvPreparationId == preparationId, cancellationToken);
        if (factor is null)
        {
            return Result.Failure<IReadOnlyList<TimeAdjustmentFactorDto>>("TIME_FACTOR_NOT_FOUND", "No such factor in this preparation.");
        }
        // A working parameter, not a record: values already adopted keep what they were adopted at.
        db.SmvTimeAdjustmentFactors.Remove(factor);
        await db.SaveChangesAsync(cancellationToken);
        await RecomputePreparationAsync(preparationId, cancellationToken);
        return Result.Success(await FactorsAsync(preparationId, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<SalesAnalysisSummaryDto>>> ListAsync(Guid preparationId, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<SalesAnalysisSummaryDto>>(await db.SalesAnalyses.AsNoTracking().Where(x => x.SmvPreparationId == preparationId)
            .OrderBy(x => x.Classification!.Name).ThenBy(x => x.ActualUse!.Name)
            .Select(x => new SalesAnalysisSummaryDto(x.Id, x.Classification!.Name, x.ActualUse != null ? x.ActualUse.Name : null, x.AreaUnit,
                x.Sales.Count, x.Sales.Count(s => s.RoundedUnitValue != null), x.Groups.Count, x.Groups.Count(g => g.AdoptedAt != null)))
            .ToListAsync(cancellationToken));

    public async Task<Result<SalesAnalysisDto>> CreateAsync(Guid preparationId, CreateSalesAnalysisRequest r, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        var municipalities = (r.MunicipalityIds ?? []).Distinct().ToList();
        if (!Enum.IsDefined(r.AreaUnit) || municipalities.Count == 0 || r.SalesFrom > r.SalesTo || r.RoundingIncrement < 0 || r.RangeWidthPercent <= 0
            || r.RangeWidthPercent > 100 || r.Notes?.Length > 2000)
        {
            return Fail("VALIDATION_FAILED",
                "A class, an area unit and at least one city/municipality are required; the sales period must not end before it starts; rounding not negative; range width above 0 and up to 100; notes max 2000.");
        }
        var open = await OpenPreparationAsync(preparationId, ct);
        if (open is not null)
        {
            return Fail(open.Code!, open.Message!);
        }
        if (!await db.Classifications.AnyAsync(x => x.Id == r.ClassificationId, ct)
            || r.ActualUseId is { } use && !await db.ActualUses.AnyAsync(x => x.Id == use, ct))
        {
            return Fail("CLASSIFICATION_NOT_FOUND", "The class or the use does not exist.");
        }
        if (await db.Municipalities.CountAsync(x => municipalities.Contains(x.Id), ct) != municipalities.Count)
        {
            return Fail("MUNICIPALITY_NOT_FOUND", "A city/municipality in the scope does not exist.");
        }
        if (await db.SalesAnalyses.AnyAsync(x => x.SmvPreparationId == preparationId && x.ClassificationId == r.ClassificationId && x.ActualUseId == r.ActualUseId, ct))
        {
            return Fail("SALES_ANALYSIS_DUPLICATE", "This class (and use) is already analysed in the preparation.");
        }
        var analysis = new SalesAnalysis
        {
            SmvPreparationId = preparationId, ClassificationId = r.ClassificationId, ActualUseId = r.ActualUseId, SalesFrom = r.SalesFrom, SalesTo = r.SalesTo,
            AreaUnit = r.AreaUnit, RoundingIncrement = r.RoundingIncrement ?? 100m, RangeWidthPercent = r.RangeWidthPercent,
            Notes = Clean(r.Notes), Scope = municipalities.Select(m => new SalesAnalysisScope { MunicipalityId = m }).ToList(),
        };
        db.SalesAnalyses.Add(analysis);
        await AddSalesAsync(analysis, ct);
        Recompute(analysis, await FactorRowsAsync(preparationId, ct));
        await db.SaveChangesAsync(ct);
        return await GetAsync(analysis.Id, ct);
    }

    public async Task<Result<SalesAnalysisDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        var a = await db.SalesAnalyses.AsNoTracking().Include(x => x.Classification).Include(x => x.ActualUse)
            .Include(x => x.Scope).ThenInclude(s => s.Municipality)
            .Include(x => x.Sales).ThenInclude(s => s.Barangay).Include(x => x.Sales).ThenInclude(s => s.SubClassification)
            .Include(x => x.Groups).ThenInclude(g => g.SubClassification)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null)
        {
            return Fail("SALES_ANALYSIS_NOT_FOUND", "No sales analysis was found with the given id.");
        }
        var preparation = await db.SmvPreparations.AsNoTracking().FirstAsync(x => x.Id == a.SmvPreparationId, ct);
        var warnings = new List<string>();
        if (!await db.SmvTimeAdjustmentFactors.AnyAsync(x => x.SmvPreparationId == a.SmvPreparationId, ct))
        {
            warnings.Add("No time-adjustment factor is entered: prices are not adjusted to the base valuation date.");
        }
        var analysed = a.Sales.Where(s => s.RoundedUnitValue != null).OrderBy(s => s.RoundedUnitValue).ThenBy(s => s.AdjustedUnitPrice).ThenBy(s => s.TransactionDate).ToList();
        var sorted = analysed.Select(s => s.RoundedUnitValue!.Value).ToList();
        var intervals = SalesAnalysisMath.Intervals(sorted);
        var average = SalesAnalysisMath.AverageInterval(sorted);
        var width = a.RangeWidthPercent ?? (average is > 0 ? average : null);
        IReadOnlyList<SalesRange> ranges = [];
        if (sorted.Count > 0 && width is { } w)
        {
            ranges = SalesAnalysisMath.Ranges(sorted, w, a.RoundingIncrement);
        }
        else if (sorted.Count > 0)
        {
            warnings.Add("Enter the range width: the average interval cannot give one.");
        }
        var groups = a.Groups.OrderByDescending(g => g.FromValue).ToList();
        Guid? GroupOf(SalesRange range) => groups.FirstOrDefault(g => range.Low >= g.FromValue && range.Low <= g.ToValue)?.Id;
        return Result.Success(new SalesAnalysisDto(
            a.Id, a.SmvPreparationId, preparation.Status, OpenStatuses.Contains(preparation.Status) && !jurisdiction.Restricted, preparation.BaseValuationDate,
            a.ClassificationId, a.Classification!.Name, a.ActualUseId, a.ActualUse?.Name, a.Scope.Select(s => s.Municipality?.Name ?? "").Order().ToList(),
            a.SalesFrom, a.SalesTo, a.AreaUnit, a.RoundingIncrement, a.RangeWidthPercent, average, width, a.Notes,
            a.Sales.OrderBy(s => s.TransactionDate).ThenBy(s => s.Id).Select(s => new SalesAnalysisSaleDto(
                s.Id, s.MarketTransactionId, s.TransactionDate, s.Barangay?.Name, s.Location, s.TaxDeclarationNumber, s.Pin, s.SubClassification?.Name,
                s.Area, s.Price, s.UnitPrice, s.TimeFactor, s.OtherAdjustmentPercent, s.AdjustedUnitPrice, s.RoundedUnitValue, s.LeftOut, s.ExclusionReason,
                s.Note)).ToList(),
            analysed.Select((s, i) => new SalesAnalysisValueDto(i + 1, s.Id, s.AdjustedUnitPrice!.Value, s.RoundedUnitValue!.Value, intervals[i])).ToList(),
            ranges.Select(r => new SalesAnalysisRangeDto(r.Number, r.Low, r.Mid, r.High, r.Frequency, GroupOf(r))).ToList(),
            groups.Select((g, i) =>
            {
                var inGroup = ranges.Where(r => r.Low >= g.FromValue && r.Low <= g.ToValue).ToList();
                return new SalesAnalysisGroupDto(g.Id, i + 1, g.FromValue, g.ToValue, g.SubClassificationId, g.SubClassification?.Name,
                    sorted.Count(v => v >= g.FromValue && v <= g.ToValue), inGroup.Count > 0 ? SalesAnalysisMath.ProposedValue(inGroup, a.RoundingIncrement) : null,
                    g.AdoptedValue, g.Basis, g.SmvScheduleId, g.AdoptedAt);
            }).ToList(),
            warnings));
    }

    public async Task<Result<SalesAnalysisDto>> UpdateAsync(Guid id, UpdateSalesAnalysisRequest r, CancellationToken cancellationToken = default)
    {
        if (r.RoundingIncrement < 0 || r.RangeWidthPercent <= 0 || r.RangeWidthPercent > 100 || r.Notes?.Length > 2000)
        {
            return Fail("VALIDATION_FAILED", "Rounding not negative; range width above 0 and up to 100; notes max 2000.");
        }
        var opened = await OpenAnalysisAsync(id, cancellationToken);
        if (opened.IsFailure)
        {
            return Fail(opened.Code!, opened.Message!);
        }
        var a = opened.Value;
        (a.RoundingIncrement, a.RangeWidthPercent, a.Notes) = (r.RoundingIncrement, r.RangeWidthPercent, Clean(r.Notes));
        Recompute(a, await FactorRowsAsync(a.SmvPreparationId, cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<SalesAnalysisDto>> RefreshAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var opened = await OpenAnalysisAsync(id, cancellationToken);
        if (opened.IsFailure)
        {
            return Fail(opened.Code!, opened.Message!);
        }
        var a = opened.Value;
        var ids = a.Sales.Select(s => s.MarketTransactionId).ToList();
        var stillAccepted = (await db.MarketTransactions.AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.CancelledAt == null && x.Review == MarketDataReview.Accepted).Select(x => x.Id).ToListAsync(cancellationToken)).ToHashSet();
        foreach (var sale in a.Sales.Where(s => !s.LeftOut && !stillAccepted.Contains(s.MarketTransactionId)))
        {
            (sale.LeftOut, sale.ExclusionReason) = (true, "No longer an accepted sale in the market data.");
        }
        await AddSalesAsync(a, cancellationToken);
        Recompute(a, await FactorRowsAsync(a.SmvPreparationId, cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<SalesAnalysisDto>> UpdateSaleAsync(Guid id, Guid saleId, UpdateAnalysisSaleRequest r, CancellationToken cancellationToken = default)
    {
        var reason = Clean(r.ExclusionReason);
        if (r.LeftOut && reason is null || reason?.Length > 500 || r.OtherAdjustmentPercent is < -100 or > 100 || r.Note?.Length > 500)
        {
            return Fail("VALIDATION_FAILED", "Leaving a sale out needs a reason (max 500); the other adjustment is from −100 to 100 percent; note max 500.");
        }
        var opened = await OpenAnalysisAsync(id, cancellationToken);
        if (opened.IsFailure)
        {
            return Fail(opened.Code!, opened.Message!);
        }
        var a = opened.Value;
        var sale = a.Sales.FirstOrDefault(s => s.Id == saleId);
        if (sale is null)
        {
            return Fail("SALES_ANALYSIS_SALE_NOT_FOUND", "The sale is not part of this analysis.");
        }
        (sale.LeftOut, sale.ExclusionReason, sale.OtherAdjustmentPercent, sale.Note) = (r.LeftOut, r.LeftOut ? reason : null, r.OtherAdjustmentPercent, Clean(r.Note));
        Recompute(a, await FactorRowsAsync(a.SmvPreparationId, cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<SalesAnalysisDto>> SetGroupsAsync(Guid id, SetSalesAnalysisGroupsRequest r, CancellationToken cancellationToken = default)
    {
        var input = (r.Groups ?? []).ToList();
        if (input.Any(g => g.FromValue < 0 || g.ToValue < g.FromValue || g.AdoptedValue <= 0 || g.Basis?.Length > 1000))
        {
            return Fail("VALIDATION_FAILED", "Each group needs from ≤ to; an adopted value above 0; basis max 1000.");
        }
        var opened = await OpenAnalysisAsync(id, cancellationToken);
        if (opened.IsFailure)
        {
            return Fail(opened.Code!, opened.Message!);
        }
        var a = opened.Value;
        var adopted = a.Groups.Where(g => g.AdoptedAt != null).ToList();
        var all = input.Select(g => (g.FromValue, g.ToValue)).Concat(adopted.Select(g => (g.FromValue, g.ToValue))).OrderBy(x => x.FromValue).ToList();
        for (var i = 1; i < all.Count; i++)
        {
            if (all[i].FromValue <= all[i - 1].ToValue)
            {
                return Fail("SALES_ANALYSIS_GROUP_OVERLAP", "Groups may not overlap, an adopted one included.");
            }
        }
        var subClasses = input.Select(g => g.SubClassificationId).OfType<Guid>().Concat(adopted.Select(g => g.SubClassificationId).OfType<Guid>()).ToList();
        if (subClasses.Count != subClasses.Distinct().Count())
        {
            return Fail("SALES_ANALYSIS_SUBCLASS_REPEATED", "A sub-class is named by two groups.");
        }
        if (subClasses.Count > 0 && await db.SubClassifications.CountAsync(x => subClasses.Contains(x.Id), cancellationToken) != subClasses.Distinct().Count())
        {
            return Fail("SUBCLASSIFICATION_NOT_FOUND", "A sub-class does not exist.");
        }
        db.SalesAnalysisGroups.RemoveRange(a.Groups.Where(g => g.AdoptedAt == null));
        foreach (var g in input)
        {
            db.SalesAnalysisGroups.Add(new SalesAnalysisGroup
            {
                SalesAnalysisId = id, FromValue = g.FromValue, ToValue = g.ToValue, SubClassificationId = g.SubClassificationId, AdoptedValue = g.AdoptedValue,
                Basis = Clean(g.Basis),
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<SalesAnalysisDto>> AdoptGroupAsync(Guid id, Guid groupId, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        var opened = await OpenAnalysisAsync(id, ct);
        if (opened.IsFailure)
        {
            return Fail(opened.Code!, opened.Message!);
        }
        var a = opened.Value;
        var group = a.Groups.FirstOrDefault(g => g.Id == groupId);
        if (group is null)
        {
            return Fail("SALES_ANALYSIS_GROUP_NOT_FOUND", "The group is not part of this analysis.");
        }
        if (group.AdoptedAt is not null)
        {
            return Fail("SALES_ANALYSIS_GROUP_ADOPTED", "The group's value is already adopted.");
        }
        if (group.SubClassificationId is not { } subClass || group.AdoptedValue is not > 0)
        {
            return Fail("SALES_ANALYSIS_GROUP_INCOMPLETE", "Name the group's sub-class and the unit value to adopt.");
        }
        var preparation = await db.SmvPreparations.Include(x => x.ProposedSmv).FirstAsync(x => x.Id == a.SmvPreparationId, ct);
        if (await db.SalesAnalysisGroups.AnyAsync(g => g.AdoptedAt != null && g.SubClassificationId == subClass && g.Id != groupId
                && db.SalesAnalyses.Any(s => s.Id == g.SalesAnalysisId && s.SmvPreparationId == preparation.Id && s.ClassificationId == a.ClassificationId
                    && s.ActualUseId == a.ActualUseId), ct))
        {
            return Fail("SUB_CLASS_ALREADY_ADOPTED", "A unit value of this sub-class (and use) is already adopted in the preparation.");
        }
        var landType = await db.PropertyTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Code == PropertyTypeCodes.Land, ct);
        if (landType is null)
        {
            return Fail("PROPERTY_TYPE_NOT_CONFIGURED", $"No PropertyType with code '{PropertyTypeCodes.Land}' is configured.");
        }
        var smv = preparation.ProposedSmv!;
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        var row = await smvs.CreateScheduleAsync(smv.Id, new CreateSmvScheduleRequest(a.ClassificationId, a.ActualUseId, landType.Id, null,
            a.AreaUnit == AreaMeasure.Hectare ? "per hectare" : "per sqm", group.AdoptedValue.Value, null, null, smv.EffectivityDate,
            SubClassificationId: subClass), ct);
        if (row.IsFailure)
        {
            return Fail(row.Code!, row.Message!);
        }
        (group.SmvScheduleId, group.AdoptedAt, group.AdoptedBy) = (row.Value.Id, clock.UtcNow, currentUser.AppUserId);
        await db.SaveChangesAsync(ct);
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }
        return await GetAsync(id, ct);
    }

    /// <summary>Copies in the accepted land sales of the class (and use), scope and period not yet in the analysis.</summary>
    private async Task AddSalesAsync(SalesAnalysis a, CancellationToken ct)
    {
        var municipalities = a.Scope.Select(s => s.MunicipalityId).ToList();
        var known = a.Sales.Select(s => s.MarketTransactionId).ToHashSet();
        var sales = await db.MarketTransactions.AsNoTracking()
            .Where(x => x.CancelledAt == null && x.Review == MarketDataReview.Accepted && x.ConveysLand && x.ClassificationId == a.ClassificationId
                && (a.ActualUseId == null || x.ActualUseId == a.ActualUseId) && municipalities.Contains(x.MunicipalityId)
                && (a.SalesFrom == null || x.TransactionDate >= a.SalesFrom) && (a.SalesTo == null || x.TransactionDate <= a.SalesTo))
            .ToListAsync(ct);
        foreach (var x in sales.Where(x => !known.Contains(x.Id)))
        {
            // Added through the set: a child added only to a loaded parent's collection would be taken for an existing row.
            var sale = new SalesAnalysisSale
            {
                SalesAnalysisId = a.Id, MarketTransactionId = x.Id, TransactionDate = x.TransactionDate, MunicipalityId = x.MunicipalityId, BarangayId = x.BarangayId,
                Location = x.Location, TaxDeclarationNumber = x.TaxDeclarationNumber, Pin = x.Pin, SubClassificationId = x.SubClassificationId,
                Area = x.LandArea is { } area ? AreaIn(area, x.LandAreaUnit, a.AreaUnit) : null,
                Price = x.ConveysBuilding ? x.LandConsideration : x.Consideration,
            };
            db.SalesAnalysisSales.Add(sale);
            a.Sales.Add(sale);
        }
    }

    private static decimal AreaIn(decimal area, AreaMeasure from, AreaMeasure to) =>
        from == to ? area : to == AreaMeasure.Hectare ? area / ValuationTesting.SquareMetresPerHectare : area * ValuationTesting.SquareMetresPerHectare;

    /// <summary>Each sale's unit price, time factor, adjusted and rounded value — or why it is not analysed.</summary>
    private static void Recompute(SalesAnalysis a, IReadOnlyList<SmvTimeAdjustmentFactor> factors)
    {
        foreach (var s in a.Sales)
        {
            s.UnitPrice = s.Price is { } p && s.Area is > 0 ? Math.Round(p / s.Area.Value, 2, MidpointRounding.AwayFromZero) : null;
            var factor = factors.FirstOrDefault(f => f.PeriodFrom <= s.TransactionDate && f.PeriodTo >= s.TransactionDate);
            s.TimeFactor = factor?.Factor ?? (factors.Count == 0 ? 1m : null);
            string? reason = s.Area is not > 0 ? "The sale records no land area."
                : s.Price is not > 0 ? "The land's price cannot be told apart from the building's."
                : s.TimeFactor is null ? $"No time-adjustment factor covers {s.TransactionDate:yyyy-MM-dd}."
                : null;
            if (s.LeftOut || reason is not null)
            {
                s.ExclusionReason = s.LeftOut ? s.ExclusionReason : reason;
                (s.AdjustedUnitPrice, s.RoundedUnitValue) = (null, null);
                continue;
            }
            s.ExclusionReason = null;
            s.AdjustedUnitPrice = SalesAnalysisMath.Adjust(s.UnitPrice!.Value, s.TimeFactor!.Value, s.OtherAdjustmentPercent);
            s.RoundedUnitValue = SalesAnalysisMath.RoundTo(s.AdjustedUnitPrice.Value, a.RoundingIncrement);
        }
    }

    private async Task RecomputePreparationAsync(Guid preparationId, CancellationToken ct)
    {
        var factors = await FactorRowsAsync(preparationId, ct);
        foreach (var a in await db.SalesAnalyses.Include(x => x.Sales).Where(x => x.SmvPreparationId == preparationId).ToListAsync(ct))
        {
            Recompute(a, factors);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<SmvTimeAdjustmentFactor>> FactorRowsAsync(Guid preparationId, CancellationToken ct) =>
        await db.SmvTimeAdjustmentFactors.AsNoTracking().Where(x => x.SmvPreparationId == preparationId).ToListAsync(ct);

    private async Task<IReadOnlyList<TimeAdjustmentFactorDto>> FactorsAsync(Guid preparationId, CancellationToken ct) =>
        await db.SmvTimeAdjustmentFactors.AsNoTracking().Where(x => x.SmvPreparationId == preparationId).OrderBy(x => x.PeriodFrom)
            .Select(x => new TimeAdjustmentFactorDto(x.Id, x.PeriodFrom, x.PeriodTo, x.Factor, x.Source)).ToListAsync(ct);

    /// <summary>Why the preparation's analyses cannot change now, or null: provincial office, before submission (or after a remand).</summary>
    private async Task<Result?> OpenPreparationAsync(Guid preparationId, CancellationToken ct)
    {
        if (jurisdiction.Restricted)
        {
            return Result.Failure(SmvPreparationService.ForbiddenCode, "The SMV is prepared by the Provincial Assessor's Office; municipal offices see the preparation only.");
        }
        var status = await db.SmvPreparations.Where(x => x.Id == preparationId).Select(x => (SmvPreparationStatus?)x.Status).FirstOrDefaultAsync(ct);
        return status switch
        {
            null => Result.Failure("SMV_PREPARATION_NOT_FOUND", "No SMV preparation was found with the given id."),
            { } s when !OpenStatuses.Contains(s) => Result.Failure("SMV_PREPARATION_STAGE",
                $"The sales analysis is done before the SMV is submitted, or after a remand; the preparation is {s}."),
            _ => null,
        };
    }

    private async Task<Result<SalesAnalysis>> OpenAnalysisAsync(Guid id, CancellationToken ct)
    {
        var a = await db.SalesAnalyses.Include(x => x.Sales).Include(x => x.Groups).Include(x => x.Scope).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null)
        {
            return Result.Failure<SalesAnalysis>("SALES_ANALYSIS_NOT_FOUND", "No sales analysis was found with the given id.");
        }
        var open = await OpenPreparationAsync(a.SmvPreparationId, ct);
        return open is null ? Result.Success(a) : Result.Failure<SalesAnalysis>(open.Code!, open.Message!);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static Result<SalesAnalysisDto> Fail(string code, string message) => Result.Failure<SalesAnalysisDto>(code, message);
}
