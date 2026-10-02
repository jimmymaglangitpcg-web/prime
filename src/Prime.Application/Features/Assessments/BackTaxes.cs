using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Valuation;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Assessments;

/// <param name="InitialAssessmentYear">The year the unit is first assessed; default this year.</param>
/// <param name="TransactionTypeId">The back-tax transaction; its effectivity rule, if any, must be Periods or Fixed.</param>
public sealed record BackTaxRequest(Guid RpuId, int DeclaredFromYear, string Basis, int? InitialAssessmentYear = null, Guid? TransactionTypeId = null);

public sealed record BackTaxPeriodDto(int Sequence, DateOnly StartDate, DateOnly? EndDate, string? SmvReference, Guid? ValuationId = null,
    Guid? AssessmentId = null, decimal? MarketValue = null, decimal? AssessedValue = null, WorkflowStatus? AssessmentStatus = null);

public sealed record BackTaxRunDto(Guid? Id, Guid RpuId, int DeclaredFromYear, string Basis, int InitialAssessmentYear, int YearsLimit,
    string YearsLimitLegalBasis, BackTaxRules BuildingRules, BackTaxRules MachineryRules, IReadOnlyList<BackTaxPeriodDto> Periods,
    DateTimeOffset? CreatedAt = null);

public interface IBackTaxService
{
    /// <summary>The periods a run would have, without saving.</summary>
    Task<Result<BackTaxRunDto>> PreviewAsync(BackTaxRequest request, CancellationToken ct = default);
    /// <summary>The run, with a valuation and a Draft assessment for each period, all or nothing.</summary>
    Task<Result<BackTaxRunDto>> CreateAsync(BackTaxRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<BackTaxRunDto>>> ListByRpuAsync(Guid rpuId, CancellationToken ct = default);
}

/// <summary>
/// Back taxes (LAM Bk III pp.78–80; LGC §222; docs/analysis/valuation-foundation.md §4.8): from the year the unit
/// should have been declared, at most the configured number of years before its initial assessment, split at each
/// SMV effectivity date. Each period is valued as of its start — land under that period's SMV, buildings and
/// machinery under the rules the settings name (Q14) — and assessed effective then, at the levels then in force.
/// The assessments are Drafts that go through the normal approval and are posted in period order; each posting
/// prepares the period's FAAS/TD, cancelling the one before (Q15). PRIME gives the assessed value per period;
/// the tax stays with the treasury (CLAUDE.md §0).
/// </summary>
public sealed class BackTaxService(IApplicationDbContext db, IValuationService valuation, IAssessmentService assessments,
    IOptions<ValuationOptions> options, IClock clock) : IBackTaxService
{
    public async Task<Result<BackTaxRunDto>> PreviewAsync(BackTaxRequest request, CancellationToken ct = default)
    {
        var plan = await PlanAsync(request, ct);
        return plan.IsFailure ? Result.Failure<BackTaxRunDto>(plan.Code!, plan.Message!) : Result.Success(plan.Value.Dto);
    }

    public async Task<Result<BackTaxRunDto>> CreateAsync(BackTaxRequest request, CancellationToken ct = default)
    {
        var planned = await PlanAsync(request, ct);
        if (planned.IsFailure)
        {
            return Result.Failure<BackTaxRunDto>(planned.Code!, planned.Message!);
        }
        var (dto, spans, initialYear) = planned.Value;
        var current = spans[^1].Start;
        var buildingRules = options.Value.BackTaxBuildingRules == BackTaxRules.Current ? current : (DateOnly?)null;
        var machineryRules = options.Value.BackTaxMachineryRules == BackTaxRules.Current ? current : (DateOnly?)null;
        var run = new BackTaxRun
        {
            RpuId = request.RpuId, DeclaredFromYear = request.DeclaredFromYear, Basis = request.Basis.Trim(), InitialAssessmentYear = initialYear,
            TransactionTypeId = request.TransactionTypeId,
        };

        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            foreach (var span in spans)
            {
                var valued = await valuation.ComputeForRpuAsync(request.RpuId, ct, span.Start, request.TransactionTypeId,
                    buildingRulesAsOf: buildingRules, machineryRulesAsOf: machineryRules);
                if (valued.IsFailure)
                {
                    return Result.Failure<BackTaxRunDto>(valued.Code!, $"Period {span.Sequence} (from {span.Start:yyyy-MM-dd}): {valued.Message}");
                }
                var assessed = await assessments.CreateAsync(new CreateAssessmentRequest(valued.Value.Id, span.Start.Year, span.Start, null, null,
                    $"Back taxes from {request.DeclaredFromYear}: period {span.Sequence} of {spans.Count}", request.TransactionTypeId), ct);
                if (assessed.IsFailure)
                {
                    return Result.Failure<BackTaxRunDto>(assessed.Code!, $"Period {span.Sequence} (from {span.Start:yyyy-MM-dd}): {assessed.Message}");
                }
                run.Periods.Add(new BackTaxPeriod
                {
                    Sequence = span.Sequence, StartDate = span.Start, EndDate = span.End, ValuationId = valued.Value.Id, AssessmentId = assessed.Value.Id,
                });
            }
            db.BackTaxRuns.Add(run);
            await db.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
        return Result.Success((await ListByRpuAsync(request.RpuId, ct)).Value.First(r => r.Id == run.Id));
    }

    public async Task<Result<IReadOnlyList<BackTaxRunDto>>> ListByRpuAsync(Guid rpuId, CancellationToken ct = default)
    {
        var runs = await db.BackTaxRuns.AsNoTracking().Include(r => r.Periods).ThenInclude(p => p.Assessment)
            .Include(r => r.Periods).ThenInclude(p => p.Valuation).ThenInclude(v => v!.Smv)
            .Where(r => r.RpuId == rpuId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return Result.Success<IReadOnlyList<BackTaxRunDto>>(runs.Select(r => Dto(r.Id, r.RpuId, r.DeclaredFromYear, r.Basis, r.InitialAssessmentYear,
            r.Periods.OrderBy(p => p.Sequence).Select(p => new BackTaxPeriodDto(p.Sequence, p.StartDate, p.EndDate, p.Valuation?.Smv?.Reference,
                p.ValuationId, p.AssessmentId, p.Assessment?.MarketValue, p.Assessment?.AssessedValue, p.Assessment?.Status)).ToList(), r.CreatedAt)).ToList());
    }

    private sealed record Plan(BackTaxRunDto Dto, IReadOnlyList<BackTaxPeriodSpan> Spans, int InitialYear);

    private async Task<Result<Plan>> PlanAsync(BackTaxRequest request, CancellationToken ct)
    {
        if (options.Value.BackTaxYearsLimit is not { } limit)
        {
            return Result.Failure<Plan>("BACK_TAX_LIMIT_NOT_CONFIGURED", "Back taxes need Valuation:BackTaxYearsLimit (LGC §222) configured.");
        }
        if (string.IsNullOrWhiteSpace(request.Basis) || request.Basis.Length > 1000)
        {
            return Result.Failure<Plan>("VALIDATION_FAILED", "Give the basis (max 1000) of the year it should have been declared from.");
        }
        var initialYear = request.InitialAssessmentYear ?? clock.Today.Year;
        if (BackTaxPeriods.StartProblem(request.DeclaredFromYear, initialYear, limit) is { } problem)
        {
            return Result.Failure<Plan>("BACK_TAX_START_INVALID", problem);
        }
        if (initialYear > clock.Today.Year)
        {
            return Result.Failure<Plan>("BACK_TAX_START_INVALID", "The year of initial assessment cannot be after this year.");
        }
        var rpu = await db.RealPropertyUnits.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.RpuId, ct);
        if (rpu is null)
        {
            return Result.Failure<Plan>("RPU_NOT_FOUND", "No RPU was found with the given id.");
        }
        // A unit declared for the first time: nothing assessed yet.
        if (await db.Assessments.AnyAsync(x => x.RpuId == rpu.Id && (x.Status == WorkflowStatus.Approved || x.Status == WorkflowStatus.Posted), ct))
        {
            return Result.Failure<Plan>("BACK_TAX_ALREADY_ASSESSED",
                "This unit already has an approved or posted assessment; back taxes apply to a unit declared for the first time or discovered.");
        }
        if (request.TransactionTypeId is { } typeId)
        {
            var type = await db.TransactionTypes.AsNoTracking().InForce(clock.Today).FirstOrDefaultAsync(x => x.Id == typeId, ct);
            if (type is null)
            {
                return Result.Failure<Plan>("TRANSACTION_TYPE_NOT_IN_FORCE", "The transaction type is not one in force today.");
            }
            if (type.EffectivityRule is not (null or EffectivityRule.Periods or EffectivityRule.Fixed))
            {
                return Result.Failure<Plan>("BACK_TAX_TRANSACTION_RULE",
                    $"A back-tax period takes effect at its own start; the {type.Code} type's rule ({type.EffectivityRule}) would move it. Use a type with the Periods rule.");
            }
        }
        // The SMVs that cover the property: their effectivity dates cut the periods (until the end of the initial year, not after today).
        var municipalityId = await db.Properties.Where(x => x.Id == rpu.PropertyId).Select(x => x.MunicipalityId).FirstOrDefaultAsync(ct);
        var until = new DateOnly(initialYear, 12, 31) < clock.Today ? new DateOnly(initialYear, 12, 31) : clock.Today;
        var smvs = await db.Smvs.AsNoTracking()
            .Where(x => x.Status == WorkflowStatus.Approved && x.EffectivityDate <= until
                && (!x.Coverage.Any() || x.Coverage.Any(c => c.MunicipalityId == municipalityId)))
            .Select(x => new { x.EffectivityDate, x.OrdinanceNumber, x.CertificationReference }).ToListAsync(ct);
        var spans = BackTaxPeriods.Split(request.DeclaredFromYear, until, smvs.Select(x => x.EffectivityDate));
        // The SMV(s) in force at each period's start; the valuation names the one that prices the unit.
        string? InForce(DateOnly start)
        {
            var latest = smvs.Where(x => x.EffectivityDate <= start).OrderByDescending(x => x.EffectivityDate).FirstOrDefault()?.EffectivityDate;
            var refs = smvs.Where(x => x.EffectivityDate == latest).Select(x => x.OrdinanceNumber ?? x.CertificationReference ?? "").Order().ToList();
            return refs.Count switch { 0 => null, 1 => refs[0], _ => $"{refs.Count} SMVs effective {latest:yyyy-MM-dd}" };
        }
        var periods = spans.Select(s => new BackTaxPeriodDto(s.Sequence, s.Start, s.End, InForce(s.Start))).ToList();
        return Result.Success(new Plan(Dto(null, rpu.Id, request.DeclaredFromYear, request.Basis.Trim(), initialYear, periods, null), spans, initialYear));
    }

    private BackTaxRunDto Dto(Guid? id, Guid rpuId, int from, string basis, int initialYear, IReadOnlyList<BackTaxPeriodDto> periods, DateTimeOffset? createdAt) =>
        new(id, rpuId, from, basis, initialYear, options.Value.BackTaxYearsLimit ?? 0, options.Value.BackTaxYearsLimitLegalBasis ?? "",
            options.Value.BackTaxBuildingRules, options.Value.BackTaxMachineryRules, periods, createdAt);
}
