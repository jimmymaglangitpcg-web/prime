using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Smv;

public sealed record CreateAdjustmentFactorRequest(
    Guid SmvId, string Code, string Name, decimal Percent, Guid? ClassificationId, string? Description,
    string LegalBasis, DateOnly EffectiveDate, string? Remarks);

public sealed record AdjustmentFactorDto(
    Guid Id, Guid SmvId, string SmvOrdinanceNumber, string Code, string Name, decimal Percent, Guid? ClassificationId, string? ClassificationName,
    string? Description, string LegalBasis, DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status,
    Guid? CreatedBy, DateTimeOffset CreatedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, string? Remarks);

public interface IAdjustmentFactorService
{
    Task<Result<AdjustmentFactorDto>> CreateAsync(CreateAdjustmentFactorRequest request, CancellationToken cancellationToken = default);
    Task<Result<AdjustmentFactorDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AdjustmentFactorDto>>> ListAsync(Guid? smvId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The SMV ordinance's market value adjustment factors (docs/analysis/mrpaao-forms-model.md
/// §8.3). A Draft is approved by a second user; approving a new version of the
/// same (SMV, code) ends the previous one. Every percentage is LGU data.
/// </summary>
public sealed class AdjustmentFactorService(IApplicationDbContext db, ICurrentUserService currentUser) : IAdjustmentFactorService
{
    public async Task<Result<AdjustmentFactorDto>> CreateAsync(CreateAdjustmentFactorRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.Code?.Trim() ?? "";
        if (code.Length is 0 or > 20 || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200
            || string.IsNullOrWhiteSpace(request.LegalBasis) || request.LegalBasis.Length > 500
            || request.Percent is <= -100m or > 1000m || request.EffectiveDate == default || request.Description?.Length > 1000)
        {
            return Fail("VALIDATION_FAILED",
                "code (max 20), name (max 200), legalBasis (max 500) and effectiveDate are required; percent must be above -100 and at most 1000.");
        }
        if (!await db.Smvs.AnyAsync(x => x.Id == request.SmvId, cancellationToken))
        {
            return Fail("SMV_NOT_FOUND", "No SMV was found with the given id.");
        }
        if (request.ClassificationId is { } c && !await db.Classifications.AnyAsync(x => x.Id == c, cancellationToken))
        {
            return Fail("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        var factor = new AdjustmentFactor
        {
            SmvId = request.SmvId, Code = code, Name = request.Name.Trim(), Percent = request.Percent, ClassificationId = request.ClassificationId,
            Description = request.Description, LegalBasis = request.LegalBasis.Trim(), EffectiveDate = request.EffectiveDate, Remarks = request.Remarks,
        };
        db.AdjustmentFactors.Add(factor);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapAsync(factor.Id, cancellationToken));
    }

    public async Task<Result<AdjustmentFactorDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var factor = await db.AdjustmentFactors.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (factor is null)
        {
            return Fail("ADJUSTMENT_FACTOR_NOT_FOUND", "No adjustment factor was found with the given id.");
        }
        if (await ConfigurationApproval.ApproveAsync(db, currentUser,
                db.AdjustmentFactors.Where(x => x.SmvId == factor.SmvId && x.Code == factor.Code), factor, "ADJUSTMENT_FACTOR", cancellationToken) is { } failure)
        {
            return Fail(failure.Code!, failure.Message!);
        }
        return Result.Success(await MapAsync(id, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<AdjustmentFactorDto>>> ListAsync(Guid? smvId, CancellationToken cancellationToken = default)
    {
        var query = Query();
        if (smvId is { } s)
        {
            query = query.Where(x => x.SmvId == s);
        }
        var rows = await query.OrderBy(x => x.Code).ThenByDescending(x => x.EffectiveDate).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<AdjustmentFactorDto>>(rows.Select(ToDto).ToList());
    }

    private IQueryable<AdjustmentFactor> Query() => db.AdjustmentFactors.AsNoTracking().Include(x => x.Smv).Include(x => x.Classification);

    private async Task<AdjustmentFactorDto> MapAsync(Guid id, CancellationToken ct) => ToDto(await Query().SingleAsync(x => x.Id == id, ct));

    private static AdjustmentFactorDto ToDto(AdjustmentFactor x) => new(
        x.Id, x.SmvId, x.Smv!.OrdinanceNumber, x.Code, x.Name, x.Percent, x.ClassificationId, x.Classification?.Name, x.Description,
        x.LegalBasis, x.EffectiveDate, x.EndDate, x.Status, x.CreatedBy, x.CreatedAt, x.ApprovedBy, x.ApprovedAt, x.Remarks);

    private static Result<AdjustmentFactorDto> Fail(string code, string message) => Result.Failure<AdjustmentFactorDto>(code, message);
}
