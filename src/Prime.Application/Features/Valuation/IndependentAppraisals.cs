using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Valuation;

public sealed record IndependentAppraisalInputRequest(string Name, decimal Value, string? Unit);

public sealed record CreateIndependentAppraisalRequest(
    Guid RpuId, IndependentAppraisalSubject Subject, Guid SubjectId, AppraisalApproach Approach, decimal Value, DateOnly AppraisedOn,
    string Basis, string Evidence, IReadOnlyList<IndependentAppraisalInputRequest>? Inputs);

public sealed record WithdrawIndependentAppraisalRequest(string Reason);

public sealed record IndependentAppraisalInputDto(int Sequence, string Name, decimal Value, string? Unit);

public sealed record IndependentAppraisalDto(
    Guid Id, Guid RpuId, IndependentAppraisalSubject Subject, Guid SubjectId, string SubjectLabel, AppraisalApproach Approach, decimal Value,
    DateOnly AppraisedOn, string Basis, string Evidence, bool IsCurrent, string? EndReason, Guid? CreatedBy, DateTimeOffset CreatedAt,
    IReadOnlyList<IndependentAppraisalInputDto> Inputs);

public interface IIndependentAppraisalService
{
    Task<Result<IndependentAppraisalDto>> CreateAsync(CreateIndependentAppraisalRequest request, CancellationToken ct = default);
    Task<Result<IndependentAppraisalDto>> WithdrawAsync(Guid id, WithdrawIndependentAppraisalRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<IndependentAppraisalDto>>> ListByRpuAsync(Guid rpuId, CancellationToken ct = default);
}

/// <summary>
/// Independent appraisals (docs/analysis/valuation-foundation.md §4.7): the appraiser's value for a subject the
/// SMV does not cover, with its approach, basis, evidence and named inputs. A new appraisal of the same subject
/// replaces the current one; a withdrawal needs a reason (audited). Nothing is deleted. The value takes effect
/// in the next valuation and is reviewed with the assessment (maker-checker).
/// </summary>
public sealed class IndependentAppraisalService(IApplicationDbContext db, ICurrentUserService currentUser) : IIndependentAppraisalService
{
    public async Task<Result<IndependentAppraisalDto>> CreateAsync(CreateIndependentAppraisalRequest request, CancellationToken ct = default)
    {
        var inputs = request.Inputs ?? [];
        if (!Enum.IsDefined(request.Subject) || !Enum.IsDefined(request.Approach) || request.Value < 0m || request.AppraisedOn == default
            || string.IsNullOrWhiteSpace(request.Basis) || request.Basis.Length > 2000 || string.IsNullOrWhiteSpace(request.Evidence) || request.Evidence.Length > 2000
            || inputs.Count > 30 || inputs.Any(i => string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 100 || i.Unit?.Length > 30)
            || inputs.Select(i => i.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != inputs.Count)
        {
            return Fail("VALIDATION_FAILED",
                "subject, approach, a value of 0 or more, appraisedOn, the basis and the evidence (max 2000 each) are required; at most 30 inputs, each named once (max 100), unit max 30.");
        }
        if (await SubjectProblemAsync(request.RpuId, request.Subject, request.SubjectId, ct) is { } problem)
        {
            return Fail(problem.Code!, problem.Message!);
        }
        var appraisal = new IndependentAppraisal
        {
            RpuId = request.RpuId, Subject = request.Subject, SubjectId = request.SubjectId, Approach = request.Approach, Value = request.Value,
            AppraisedOn = request.AppraisedOn, Basis = request.Basis.Trim(), Evidence = request.Evidence.Trim(),
            Inputs = inputs.Select((i, n) => new IndependentAppraisalInput
            {
                Sequence = n + 1, Name = i.Name.Trim(), Value = i.Value, Unit = string.IsNullOrWhiteSpace(i.Unit) ? null : i.Unit.Trim(),
            }).ToList(),
        };
        // The one-current index is not deferrable: end the predecessor first.
        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            if (await db.IndependentAppraisals.FirstOrDefaultAsync(x => x.Subject == request.Subject && x.SubjectId == request.SubjectId && x.IsCurrent, ct)
                is { } current)
            {
                current.IsCurrent = false;
                current.EndReason = $"Replaced by the appraisal of {request.AppraisedOn:yyyy-MM-dd}.";
                await db.SaveChangesAsync(ct);
            }
            db.IndependentAppraisals.Add(appraisal);
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
        return Result.Success(await MapAsync(appraisal.Id, ct));
    }

    public async Task<Result<IndependentAppraisalDto>> WithdrawAsync(Guid id, WithdrawIndependentAppraisalRequest request, CancellationToken ct = default)
    {
        var appraisal = await db.IndependentAppraisals.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (appraisal is null)
        {
            return Fail("INDEPENDENT_APPRAISAL_NOT_FOUND", "No independent appraisal was found with the given id.");
        }
        if (!appraisal.IsCurrent)
        {
            return Fail("INDEPENDENT_APPRAISAL_NOT_CURRENT", "Only the current appraisal of a subject can be withdrawn.");
        }
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
        {
            return Fail("VALIDATION_FAILED", "A reason (max 500) is required.");
        }
        appraisal.IsCurrent = false;
        appraisal.EndReason = request.Reason.Trim();
        currentUser.Reason = appraisal.EndReason;
        await db.SaveChangesAsync(ct);
        return Result.Success(await MapAsync(id, ct));
    }

    public async Task<Result<IReadOnlyList<IndependentAppraisalDto>>> ListByRpuAsync(Guid rpuId, CancellationToken ct = default)
    {
        var rows = await db.IndependentAppraisals.AsNoTracking().Include(x => x.Inputs).Where(x => x.RpuId == rpuId)
            .OrderByDescending(x => x.IsCurrent).ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
        var labels = await LabelsAsync(rows, ct);
        return Result.Success<IReadOnlyList<IndependentAppraisalDto>>(rows.Select(x => ToDto(x, labels)).ToList());
    }

    /// <summary>The subject must be the unit's own land, building, additional item or machine.</summary>
    private async Task<Result?> SubjectProblemAsync(Guid rpuId, IndependentAppraisalSubject subject, Guid subjectId, CancellationToken ct)
    {
        if (await db.RealPropertyUnits.FirstOrDefaultAsync(x => x.Id == rpuId, ct) is null)
        {
            return Result.Failure("RPU_NOT_FOUND", "No RPU was found with the given id.");
        }
        var ok = subject switch
        {
            IndependentAppraisalSubject.Land => await db.Lands.AnyAsync(x => x.Id == subjectId && x.RpuId == rpuId, ct),
            IndependentAppraisalSubject.Building => await db.Buildings.AnyAsync(x => x.Id == subjectId && x.RpuId == rpuId, ct),
            IndependentAppraisalSubject.BuildingComponent => await db.BuildingComponents
                .AnyAsync(c => c.Id == subjectId && c.IsAdditionalItem && db.Buildings.Any(b => b.Id == c.BuildingId && b.RpuId == rpuId), ct),
            IndependentAppraisalSubject.Machinery => await db.MachineryUnits.AnyAsync(x => x.Id == subjectId && x.RpuId == rpuId, ct),
            _ => false,
        };
        return ok ? null : Result.Failure("APPRAISAL_SUBJECT_INVALID",
            "The subject must be this unit's land, building, additional item of its building, or machine.");
    }

    private async Task<Dictionary<Guid, string>> LabelsAsync(IReadOnlyCollection<IndependentAppraisal> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.SubjectId).Distinct().ToList();
        var labels = new Dictionary<Guid, string>();
        foreach (var x in await db.Lands.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct))
        {
            labels[x] = "Land";
        }
        foreach (var x in await db.Buildings.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, Name = x.StructuralType!.Name }).ToListAsync(ct))
        {
            labels[x.Id] = $"Building ({x.Name})";
        }
        foreach (var x in await db.BuildingComponents.AsNoTracking().Where(x => ids.Contains(x.Id))
                     .Select(x => new { x.Id, Type = x.ComponentType!.Name, x.Description }).ToListAsync(ct))
        {
            labels[x.Id] = $"Extra item: {x.Type}{(x.Description is null ? "" : $" ({x.Description})")}";
        }
        foreach (var x in await db.MachineryUnits.AsNoTracking().Where(x => ids.Contains(x.Id))
                     .Select(x => new { x.Id, x.Brand, x.Model, x.SerialNumber }).ToListAsync(ct))
        {
            var name = string.Join(" ", new[] { x.Brand, x.Model, x.SerialNumber }.Where(s => !string.IsNullOrWhiteSpace(s)));
            labels[x.Id] = $"Machine{(name.Length > 0 ? $": {name}" : "")}";
        }
        return labels;
    }

    private async Task<IndependentAppraisalDto> MapAsync(Guid id, CancellationToken ct)
    {
        var row = await db.IndependentAppraisals.AsNoTracking().Include(x => x.Inputs).SingleAsync(x => x.Id == id, ct);
        return ToDto(row, await LabelsAsync([row], ct));
    }

    private static IndependentAppraisalDto ToDto(IndependentAppraisal x, Dictionary<Guid, string> labels) => new(
        x.Id, x.RpuId, x.Subject, x.SubjectId, labels.GetValueOrDefault(x.SubjectId, x.Subject.ToString()), x.Approach, x.Value, x.AppraisedOn,
        x.Basis, x.Evidence, x.IsCurrent, x.EndReason, x.CreatedBy, x.CreatedAt,
        x.Inputs.OrderBy(i => i.Sequence).Select(i => new IndependentAppraisalInputDto(i.Sequence, i.Name, i.Value, i.Unit)).ToList());

    private static Result<IndependentAppraisalDto> Fail(string code, string message) => Result.Failure<IndependentAppraisalDto>(code, message);
}
