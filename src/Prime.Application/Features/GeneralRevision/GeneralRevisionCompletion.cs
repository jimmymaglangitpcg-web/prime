using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.GeneralRevision;

public sealed record CreateChecklistStepDefinitionRequest(
    string LegalBasis, DateOnly EffectiveDate, string? Remarks, string Code, int Sequence, string Title, string? Description, GeneralRevisionGate? Gate);

public sealed record ChecklistStepDefinitionDto(
    Guid Id, string Code, int Sequence, string Title, string? Description, GeneralRevisionGate? Gate, string LegalBasis, DateOnly EffectiveDate,
    DateOnly? EndDate, WorkflowStatus Status, Guid? CreatedBy, DateTimeOffset CreatedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, string? Remarks);

public sealed class CreateChecklistStepDefinitionRequestValidator : AbstractValidator<CreateChecklistStepDefinitionRequest>
{
    public CreateChecklistStepDefinitionRequestValidator()
    {
        RuleFor(x => x.LegalBasis).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("effectiveDate is required.");
        RuleFor(x => x.Remarks).MaximumLength(1000);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9][A-Za-z0-9_.-]*$").WithMessage("code: letters, digits, '.', '-' or '_'.");
        RuleFor(x => x.Sequence).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Gate).IsInEnum().When(x => x.Gate is not null);
    }
}

public sealed record ExcludeGeneralRevisionItemRequest(string Reason);

public sealed record CompleteChecklistStepRequest(DateOnly CompletedOn, string? Evidence);

/// <param name="Met">For a gate step, whether PRIME finds the condition met; for a manual step, whether it is marked done.</param>
public sealed record ChecklistStepDto(
    Guid Id, string Code, int Sequence, string Title, string? Description, GeneralRevisionGate? Gate, bool Met, string? GateDetail,
    DateOnly? CompletedOn, string? CompletedByName, string? Evidence);

public sealed record GateStatusDto(GeneralRevisionGate Gate, bool Met, string Detail);

/// <param name="Blockers">What stops completion; empty when the revision can be completed.</param>
public sealed record GeneralRevisionReadinessDto(IReadOnlyList<GateStatusDto> Gates, IReadOnlyList<ChecklistStepDto> Checklist, bool ChecklistLoaded,
    IReadOnlyList<string> Blockers);

public interface IGeneralRevisionCompletionService
{
    Task<Result<ChecklistStepDefinitionDto>> CreateStepDefinitionAsync(CreateChecklistStepDefinitionRequest request, CancellationToken cancellationToken = default);
    Task<Result<ChecklistStepDefinitionDto>> ApproveStepDefinitionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ChecklistStepDefinitionDto>>> ListStepDefinitionsAsync(bool inForceOnly, CancellationToken cancellationToken = default);

    /// <summary>Takes the unit out of the revision with a reason (its draft assessment, if any, is cancelled); refused once in review.</summary>
    Task<Result<GeneralRevisionItemDto>> ExcludeItemAsync(Guid id, Guid itemId, ExcludeGeneralRevisionItemRequest request, CancellationToken cancellationToken = default);
    /// <summary>Puts an excluded unit back, Pending.</summary>
    Task<Result<GeneralRevisionItemDto>> IncludeItemAsync(Guid id, Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Copies the checklist template in force today into the revision (once).</summary>
    Task<Result<GeneralRevisionReadinessDto>> LoadChecklistAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<GeneralRevisionReadinessDto>> CompleteStepAsync(Guid id, Guid stepId, CompleteChecklistStepRequest request, CancellationToken cancellationToken = default);
    /// <summary>The gates, the checklist and what stops completion.</summary>
    Task<Result<GeneralRevisionReadinessDto>> ReadinessAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Completes the revision when nothing blocks it (GRI 19).</summary>
    Task<Result<GeneralRevisionDto>> CompleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Closing a general revision (docs/analysis/smv-preparation-general-revision.md §4.6, L6-6c): units taken out with a reason, the
/// GRI checklist as content with the steps PRIME checks itself (Q13), and completion once every gate holds.
/// </summary>
public sealed class GeneralRevisionCompletionService(
    IApplicationDbContext db, IClock clock, ICurrentUserService currentUser, IJurisdiction jurisdiction,
    IValidator<CreateChecklistStepDefinitionRequest> validator, IGeneralRevisionProgrammeService programmes, IGeneralRevisionRecordsService records)
    : IGeneralRevisionCompletionService
{
    public const string CompletionReportForm = "GR_COMPLETION_REPORT";
    public const string StatusReportForm = "GR_STATUS_REPORT";

    /// <summary>The gates completion requires; the rest (Ownership Record Forms, the completion report) follow or are checklist steps.</summary>
    private static readonly GeneralRevisionGate[] RequiredForCompletion =
    [
        GeneralRevisionGate.Compiled, GeneralRevisionGate.Valued, GeneralRevisionGate.Approved, GeneralRevisionGate.Posted,
        GeneralRevisionGate.TaxDeclarationsApproved, GeneralRevisionGate.NoticesServed, GeneralRevisionGate.AssessmentRollRun,
    ];

    // --- Checklist template (configuration) ---

    public async Task<Result<ChecklistStepDefinitionDto>> CreateStepDefinitionAsync(CreateChecklistStepDefinitionRequest r, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(r, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ChecklistStepDefinitionDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        var step = new GeneralRevisionChecklistStepDefinition
        {
            LegalBasis = r.LegalBasis.Trim(), EffectiveDate = r.EffectiveDate, Remarks = Clean(r.Remarks), Code = r.Code.Trim(), Sequence = r.Sequence,
            Title = r.Title.Trim(), Description = Clean(r.Description), Gate = r.Gate,
        };
        db.GeneralRevisionChecklistStepDefinitions.Add(step);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(step));
    }

    public async Task<Result<ChecklistStepDefinitionDto>> ApproveStepDefinitionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var step = await db.GeneralRevisionChecklistStepDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (step is null)
        {
            return Result.Failure<ChecklistStepDefinitionDto>("CHECKLIST_STEP_NOT_FOUND", "No checklist step was found with the given id.");
        }
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, db.GeneralRevisionChecklistStepDefinitions.Where(x => x.Code == step.Code),
                step, "CHECKLIST_STEP", cancellationToken) is { } failure)
        {
            return Result.Failure<ChecklistStepDefinitionDto>(failure.Code!, failure.Message!);
        }
        return Result.Success(ToDto(step));
    }

    public async Task<Result<IReadOnlyList<ChecklistStepDefinitionDto>>> ListStepDefinitionsAsync(bool inForceOnly, CancellationToken cancellationToken = default)
    {
        var query = db.GeneralRevisionChecklistStepDefinitions.AsNoTracking();
        if (inForceOnly)
        {
            query = query.InForce(clock.Today);
        }
        var steps = await query.OrderBy(x => x.Sequence).ThenBy(x => x.Code).ThenByDescending(x => x.EffectiveDate).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ChecklistStepDefinitionDto>>(steps.Select(ToDto).ToList());
    }

    // --- Units taken out ---

    public async Task<Result<GeneralRevisionItemDto>> ExcludeItemAsync(Guid id, Guid itemId, ExcludeGeneralRevisionItemRequest r, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 1000)
        {
            return Result.Failure<GeneralRevisionItemDto>("VALIDATION_FAILED", "A reason is required (max 1000).");
        }
        var item = await OpenItemAsync(id, itemId, cancellationToken);
        if (item.IsFailure)
        {
            return Result.Failure<GeneralRevisionItemDto>(item.Code!, item.Message!);
        }
        var x = item.Value;
        if (x.Assessment is { Status: not (WorkflowStatus.Draft or WorkflowStatus.Rejected or WorkflowStatus.Cancelled) } busy)
        {
            return Result.Failure<GeneralRevisionItemDto>("GENERAL_REVISION_ITEM_IN_REVIEW", $"Its assessment is {busy.Status}; a unit is taken out only before review.");
        }
        if (x.Assessment is { Status: WorkflowStatus.Draft } draft)
        {
            draft.Status = WorkflowStatus.Cancelled;
        }
        (x.Status, x.ExclusionReason, x.FailureReason) = (GeneralRevisionItemStatus.Excluded, r.Reason.Trim(), null);
        currentUser.Reason = r.Reason.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return await ItemAsync(id, itemId, cancellationToken);
    }

    public async Task<Result<GeneralRevisionItemDto>> IncludeItemAsync(Guid id, Guid itemId, CancellationToken cancellationToken = default)
    {
        var item = await OpenItemAsync(id, itemId, cancellationToken);
        if (item.IsFailure)
        {
            return Result.Failure<GeneralRevisionItemDto>(item.Code!, item.Message!);
        }
        if (item.Value.Status != GeneralRevisionItemStatus.Excluded)
        {
            return Result.Failure<GeneralRevisionItemDto>("GENERAL_REVISION_ITEM_NOT_EXCLUDED", "The unit is not excluded.");
        }
        (item.Value.Status, item.Value.ExclusionReason) = (GeneralRevisionItemStatus.Pending, null);
        await db.SaveChangesAsync(cancellationToken);
        return await ItemAsync(id, itemId, cancellationToken);
    }

    // --- Checklist and completion ---

    public async Task<Result<GeneralRevisionReadinessDto>> LoadChecklistAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = await OpenAsync(id, cancellationToken);
        if (p.IsFailure)
        {
            return Result.Failure<GeneralRevisionReadinessDto>(p.Code!, p.Message!);
        }
        if (await db.GeneralRevisionChecklistSteps.AnyAsync(x => x.GeneralRevisionProgrammeId == id, cancellationToken))
        {
            return Result.Failure<GeneralRevisionReadinessDto>("CHECKLIST_ALREADY_LOADED", "The revision's checklist is already loaded.");
        }
        var template = await db.GeneralRevisionChecklistStepDefinitions.AsNoTracking().InForce(clock.Today)
            .OrderBy(x => x.Sequence).ThenBy(x => x.Code).ToListAsync(cancellationToken);
        if (template.Count == 0)
        {
            return Result.Failure<GeneralRevisionReadinessDto>("CHECKLIST_NOT_CONFIGURED",
                "No checklist step is in force: load the general revision instructions' checklist as content, and have it approved.");
        }
        db.GeneralRevisionChecklistSteps.AddRange(template.Select(t => new GeneralRevisionChecklistStep
        {
            GeneralRevisionProgrammeId = id, DefinitionId = t.Id, Code = t.Code, Sequence = t.Sequence, Title = t.Title, Description = t.Description, Gate = t.Gate,
        }));
        await db.SaveChangesAsync(cancellationToken);
        return await ReadinessAsync(id, cancellationToken);
    }

    public async Task<Result<GeneralRevisionReadinessDto>> CompleteStepAsync(Guid id, Guid stepId, CompleteChecklistStepRequest r, CancellationToken cancellationToken = default)
    {
        if (r.CompletedOn == default || r.CompletedOn > clock.Today || r.Evidence?.Length > 500)
        {
            return Result.Failure<GeneralRevisionReadinessDto>("VALIDATION_FAILED", "The date done is required and not in the future; evidence max 500.");
        }
        var p = await OpenAsync(id, cancellationToken);
        if (p.IsFailure)
        {
            return Result.Failure<GeneralRevisionReadinessDto>(p.Code!, p.Message!);
        }
        var step = await db.GeneralRevisionChecklistSteps.FirstOrDefaultAsync(x => x.Id == stepId && x.GeneralRevisionProgrammeId == id, cancellationToken);
        if (step is null)
        {
            return Result.Failure<GeneralRevisionReadinessDto>("CHECKLIST_STEP_NOT_FOUND", "No such step in the revision's checklist.");
        }
        if (step.Gate is not null)
        {
            return Result.Failure<GeneralRevisionReadinessDto>("CHECKLIST_STEP_IS_GATE", "PRIME checks this step itself; it is done when its condition holds.");
        }
        (step.CompletedOn, step.Evidence, step.CompletedBy) = (r.CompletedOn, Clean(r.Evidence), currentUser.AppUserId);
        await db.SaveChangesAsync(cancellationToken);
        return await ReadinessAsync(id, cancellationToken);
    }

    public async Task<Result<GeneralRevisionReadinessDto>> ReadinessAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        if (!await db.GeneralRevisionProgrammes.AnyAsync(x => x.Id == id, ct))
        {
            return Result.Failure<GeneralRevisionReadinessDto>("GENERAL_REVISION_NOT_FOUND", "No general revision was found with the given id.");
        }
        var gates = await GatesAsync(id, ct);
        var byGate = gates.ToDictionary(g => g.Gate);
        var steps = await db.GeneralRevisionChecklistSteps.AsNoTracking().Where(x => x.GeneralRevisionProgrammeId == id)
            .OrderBy(x => x.Sequence).ThenBy(x => x.Code)
            .Select(x => new { Step = x, By = db.AppUsers.Where(u => u.Id == x.CompletedBy).Select(u => u.DisplayName).FirstOrDefault() })
            .ToListAsync(ct);
        var checklist = steps.Select(s => new ChecklistStepDto(s.Step.Id, s.Step.Code, s.Step.Sequence, s.Step.Title, s.Step.Description, s.Step.Gate,
            s.Step.Gate is { } g ? byGate[g].Met : s.Step.CompletedOn is not null, s.Step.Gate is { } h ? byGate[h].Detail : null,
            s.Step.CompletedOn, s.By, s.Step.Evidence)).ToList();
        var blockers = gates.Where(g => RequiredForCompletion.Contains(g.Gate) && !g.Met).Select(g => g.Detail).ToList();
        // Manual steps of the loaded checklist that come before completion: every one without a gate that PRIME cannot check after it.
        blockers.AddRange(checklist.Where(s => s.Gate is null && !s.Met).Select(s => $"Checklist step {s.Code} ({s.Title}) is not marked done."));
        return Result.Success(new GeneralRevisionReadinessDto(gates, checklist, steps.Count > 0, blockers));
    }

    public async Task<Result<GeneralRevisionDto>> CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = await OpenAsync(id, cancellationToken);
        if (p.IsFailure)
        {
            return Result.Failure<GeneralRevisionDto>(p.Code!, p.Message!);
        }
        if (await db.GeneralRevisionJobs.AnyAsync(x => x.GeneralRevisionProgrammeId == id
                && (x.Status == JobExecutionStatus.Queued || x.Status == JobExecutionStatus.Running), cancellationToken))
        {
            return Result.Failure<GeneralRevisionDto>("GENERAL_REVISION_RUN_ACTIVE", "A run is queued or running.");
        }
        var readiness = (await ReadinessAsync(id, cancellationToken)).Value;
        if (readiness.Blockers.Count > 0)
        {
            return Result.Failure<GeneralRevisionDto>("GENERAL_REVISION_NOT_READY", string.Join(" ", readiness.Blockers));
        }
        (p.Value.Status, p.Value.CompletedAt) = (GeneralRevisionStatus.Completed, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return await programmes.GetAsync(id, cancellationToken);
    }

    /// <summary>Every gate's state, from the revision's own records (units, assessments, TDs, notices, register runs, issued forms).</summary>
    private async Task<List<GateStatusDto>> GatesAsync(Guid id, CancellationToken ct)
    {
        var all = db.GeneralRevisionItems.AsNoTracking().Where(x => x.GeneralRevisionProgrammeId == id);
        var items = all.Where(x => x.Status != GeneralRevisionItemStatus.Excluded);
        var total = await all.CountAsync(ct);
        var counted = await items.CountAsync(ct);
        var unvalued = await items.CountAsync(x => x.Status != GeneralRevisionItemStatus.Assessed, ct);
        var unapproved = await items.CountAsync(x => x.Assessment == null
            || (x.Assessment.Status != WorkflowStatus.Approved && x.Assessment.Status != WorkflowStatus.Posted), ct);
        var unposted = await items.CountAsync(x => x.Assessment == null || x.Assessment.Status != WorkflowStatus.Posted, ct);
        var undeclared = await items.CountAsync(x => x.Assessment == null || x.Assessment.Status != WorkflowStatus.Posted
            || !db.TaxDeclarations.Any(t => t.AssessmentId == x.AssessmentId && t.Status == WorkflowStatus.Approved), ct);
        var gates = (await records.RollGatesAsync(id, ct)).Value;
        var outstanding = gates.Sum(g => g.NoticesOutstanding);
        var waiting = gates.Where(g => !g.Open).Select(g => g.MunicipalityName).ToList();
        var barangays = await items.Select(x => x.BarangayId).Distinct().ToListAsync(ct);
        var rolled = await db.RegisterRuns.AsNoTracking()
            .Where(r => r.GeneralRevisionProgrammeId == id && r.Kind == RegisterKind.AssessmentRollTaxable && r.BarangayId != null)
            .Select(r => r.BarangayId!.Value).Distinct().ToListAsync(ct);
        var unrolled = barangays.Except(rolled).Count();
        var orf = await db.RegisterRuns.AnyAsync(r => r.GeneralRevisionProgrammeId == id && r.Kind == RegisterKind.OwnershipRecordCard, ct);
        var report = await db.IssuedForms.AnyAsync(f => f.SubjectType == FormSubjectType.GeneralRevision && f.SubjectId == id
            && f.FormCode == CompletionReportForm, ct);
        return
        [
            new(GeneralRevisionGate.Compiled, total > 0, total > 0 ? $"{total} unit(s) compiled." : "No unit is compiled yet."),
            new(GeneralRevisionGate.Valued, counted > 0 && unvalued == 0, unvalued == 0 ? "Every unit is valued." : $"{unvalued} unit(s) not valued (pending or failed)."),
            new(GeneralRevisionGate.Approved, counted > 0 && unapproved == 0, unapproved == 0 ? "Every assessment is approved." : $"{unapproved} assessment(s) not approved."),
            new(GeneralRevisionGate.Posted, counted > 0 && unposted == 0, unposted == 0 ? "Every assessment is posted." : $"{unposted} assessment(s) not posted."),
            new(GeneralRevisionGate.TaxDeclarationsApproved, counted > 0 && undeclared == 0,
                undeclared == 0 ? "Every unit is declared by an approved Tax Declaration." : $"{undeclared} unit(s) without an approved new Tax Declaration."),
            new(GeneralRevisionGate.NoticesServed, counted > 0 && unposted == 0 && outstanding == 0,
                outstanding == 0 ? "Every notice the units need is served." : $"{outstanding} unit(s) whose notice is not served."),
            new(GeneralRevisionGate.RollWaitElapsed, counted > 0 && waiting.Count == 0,
                waiting.Count == 0 ? "The roll gate is open everywhere." : $"The roll gate is closed in {string.Join(", ", waiting)}."),
            new(GeneralRevisionGate.AssessmentRollRun, counted > 0 && unrolled == 0,
                unrolled == 0 ? "A taxable assessment roll is run for every barangay." : $"{unrolled} barangay(s) without the revision's taxable assessment roll."),
            new(GeneralRevisionGate.OwnershipRecordsRun, orf, orf ? "Ownership Record Forms are run." : "No Ownership Record Form run yet."),
            new(GeneralRevisionGate.CompletionReportIssued, report, report ? "The completion report is issued." : "The completion report is not issued."),
        ];
    }

    private async Task<Result<GeneralRevisionProgramme>> OpenAsync(Guid id, CancellationToken ct)
    {
        var p = await db.GeneralRevisionProgrammes.Include(x => x.Scope).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null)
        {
            return Result.Failure<GeneralRevisionProgramme>("GENERAL_REVISION_NOT_FOUND", "No general revision was found with the given id.");
        }
        if (p.Scope.Any(s => !jurisdiction.Allows(s.MunicipalityId)))
        {
            return Result.Failure<GeneralRevisionProgramme>(JurisdictionErrors.Code, "This revision covers a city/municipality outside your office's jurisdiction.");
        }
        return p.Status is GeneralRevisionStatus.Completed or GeneralRevisionStatus.Cancelled
            ? Result.Failure<GeneralRevisionProgramme>("GENERAL_REVISION_CLOSED", $"The revision is {p.Status}.")
            : Result.Success(p);
    }

    private async Task<Result<GeneralRevisionItem>> OpenItemAsync(Guid id, Guid itemId, CancellationToken ct)
    {
        var p = await OpenAsync(id, ct);
        if (p.IsFailure)
        {
            return Result.Failure<GeneralRevisionItem>(p.Code!, p.Message!);
        }
        if (await db.GeneralRevisionJobs.AnyAsync(x => x.GeneralRevisionProgrammeId == id
                && (x.Status == JobExecutionStatus.Queued || x.Status == JobExecutionStatus.Running), ct))
        {
            return Result.Failure<GeneralRevisionItem>("GENERAL_REVISION_RUN_ACTIVE", "A run is queued or running.");
        }
        var item = await db.GeneralRevisionItems.Include(x => x.Assessment).FirstOrDefaultAsync(x => x.Id == itemId && x.GeneralRevisionProgrammeId == id, ct);
        return item is null
            ? Result.Failure<GeneralRevisionItem>("GENERAL_REVISION_ITEM_NOT_FOUND", "An item is not part of this revision.")
            : Result.Success(item);
    }

    private async Task<Result<GeneralRevisionItemDto>> ItemAsync(Guid id, Guid itemId, CancellationToken ct)
    {
        var page = await programmes.SearchItemsAsync(id, new GeneralRevisionItemSearchRequest { ItemId = itemId }, ct);
        return page.IsFailure ? Result.Failure<GeneralRevisionItemDto>(page.Code!, page.Message!) : Result.Success(page.Value.Items.Single());
    }

    private static ChecklistStepDefinitionDto ToDto(GeneralRevisionChecklistStepDefinition x) => new(
        x.Id, x.Code, x.Sequence, x.Title, x.Description, x.Gate, x.LegalBasis, x.EffectiveDate, x.EndDate, x.Status, x.CreatedBy, x.CreatedAt,
        x.ApprovedBy, x.ApprovedAt, x.Remarks);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
