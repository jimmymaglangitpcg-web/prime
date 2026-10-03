using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Approvals;
using Prime.Application.Features.Exemptions;
using Prime.Application.Features.Numbering;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.TaxDeclarations;

public sealed class TaxDeclarationService(IApplicationDbContext db, IValidator<CreateTaxDeclarationRequest> validator, INumberingService numbering, IClock clock,
    ICurrentUserService currentUser, IApprovalChainService approvals, IOptions<FaasOptions> faas, Submissions.IApprovedDocumentIssuer issuer) : ITaxDeclarationService
{
    public async Task<Result<TaxDeclarationDto>> CreateAsync(CreateTaxDeclarationRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<TaxDeclarationDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var rpu = await db.RealPropertyUnits.FirstOrDefaultAsync(r => r.Id == request.RpuId, cancellationToken);
        if (rpu is null)
        {
            return Result.Failure<TaxDeclarationDto>("RPU_NOT_FOUND", "No RPU was found with the given id.");
        }
        // The FAAS transaction code: the transaction's, a code named, and GR for a general revision
        // assessment; the highest rank wins (MRPAAO p.145, 167).
        var codes = new List<(string? Code, int? Rank)>();
        Domain.Entities.Transactions.PropertyTransaction? tx = null;
        if (request.PropertyTransactionId is { } transactionId)
        {
            tx = await db.PropertyTransactions.Include(x => x.RelatedProperties).Include(x => x.TransactionType)
                .FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken);
            if (tx is null)
            {
                return Result.Failure<TaxDeclarationDto>("PROPERTY_TRANSACTION_NOT_FOUND", "The specified property transaction does not exist.");
            }
            if (tx.Status != WorkflowStatus.Draft)
            {
                return Result.Failure<TaxDeclarationDto>("PROPERTY_TRANSACTION_NOT_DRAFT", "TDs can be added only while the transaction is a Draft.");
            }
            if (tx.PropertyId != rpu.PropertyId && tx.RelatedProperties.All(r => r.PropertyId != rpu.PropertyId))
            {
                return Result.Failure<TaxDeclarationDto>("PROPERTY_TRANSACTION_OTHER_PROPERTY",
                    "The RPU's property is not the transaction's property or one of its related properties.");
            }
            codes.Add((tx.TypeCode, tx.TransactionType!.Rank));
        }
        if (!string.IsNullOrWhiteSpace(request.TransactionCode))
        {
            var namedCode = request.TransactionCode.Trim();
            var type = await db.TransactionTypes.InForce(clock.Today).Where(x => x.Code == namedCode).Select(x => new { x.Code, x.Rank })
                .FirstOrDefaultAsync(cancellationToken);
            if (type is null)
            {
                return Result.Failure<TaxDeclarationDto>("TRANSACTION_CODE_NOT_IN_FORCE", $"'{namedCode}' is not the code of a transaction type in force.");
            }
            codes.Add((type.Code, type.Rank));
        }
        if (!await db.Classifications.AnyAsync(x => x.Id == request.ClassificationId, cancellationToken))
        {
            return Result.Failure<TaxDeclarationDto>("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        if (!await db.ActualUses.AnyAsync(x => x.Id == request.ActualUseId, cancellationToken))
        {
            return Result.Failure<TaxDeclarationDto>("ACTUAL_USE_NOT_FOUND", "The specified actual use does not exist.");
        }

        var revisionNumber = 1;
        if (request.PreviousTaxDeclarationId is not null)
        {
            var previous = await db.TaxDeclarations.FirstOrDefaultAsync(td => td.Id == request.PreviousTaxDeclarationId, cancellationToken);
            if (previous is null)
            {
                return Result.Failure<TaxDeclarationDto>("PREVIOUS_TAX_DECLARATION_NOT_FOUND", "The specified previous Tax Declaration does not exist.");
            }
            // A relocated machine's new unit names the TD of the unit it continues, on the property it left (Q7).
            var relocation = tx is { Kind: PropertyTransactionKind.MachineryRelocation } && previous.RpuId == tx.RelocatedRpuId
                && rpu.PreviousRpuId == tx.RelocatedRpuId;
            if (previous.RpuId != request.RpuId && !relocation)
            {
                return Result.Failure<TaxDeclarationDto>("PREVIOUS_TAX_DECLARATION_OTHER_RPU", "The previous Tax Declaration belongs to a different RPU.");
            }
            if (previous.Status is WorkflowStatus.Cancelled or WorkflowStatus.Voided)
            {
                return Result.Failure<TaxDeclarationDto>("PREVIOUS_TAX_DECLARATION_CANCELLED",
                    $"TD {previous.TaxDeclarationNumber} is already cancelled; name the current TD instead.");
            }
            revisionNumber = previous.RevisionNumber + 1;
        }
        // A court order restores a cancelled declaration by a new TD naming it (assessment-listing-exemptions.md §4.3).
        if (request.RestoresTaxDeclarationId is { } restoredId)
        {
            if (tx is not { Kind: PropertyTransactionKind.CourtOrder })
            {
                return Result.Failure<TaxDeclarationDto>("TAX_DECLARATION_RESTORE_NOT_ALLOWED", "Only a court-order transaction restores a cancelled Tax Declaration.");
            }
            var restored = await db.TaxDeclarations.FirstOrDefaultAsync(x => x.Id == restoredId, cancellationToken);
            if (restored is null || restored.RpuId != request.RpuId || restored.Status != WorkflowStatus.Cancelled)
            {
                return Result.Failure<TaxDeclarationDto>("TAX_DECLARATION_RESTORE_INVALID", "The TD to restore must be a cancelled TD of the same RPU.");
            }
            revisionNumber = Math.Max(revisionNumber,
                await db.TaxDeclarations.Where(x => x.RpuId == request.RpuId).MaxAsync(x => x.RevisionNumber, cancellationToken) + 1);
        }

        // The assessment this TD declares (the TD with it is the FAAS): named, or the one in force.
        var assessmentId = request.AssessmentId;
        if (assessmentId is { } named)
        {
            if (await FaasTaxDeclarations.AssessmentProblemAsync(db, request.RpuId, named, cancellationToken) is { } problem)
            {
                return Result.Failure<TaxDeclarationDto>("TAX_DECLARATION_ASSESSMENT_INVALID", problem);
            }
        }
        else
        {
            assessmentId = await FaasTaxDeclarations.AssessmentInForceAsync(db, request.RpuId, request.EffectivityDate, cancellationToken);
        }
        if (assessmentId is { } declared)
        {
            codes.Add(await TransactionCodes.FromAssessmentAsync(db, faas.Value,
                await db.Assessments.FirstOrDefaultAsync(x => x.Id == declared, cancellationToken), clock.Today, cancellationToken));
        }
        var (transactionCode, transactionRank) = TransactionCodes.Highest(codes);

        // Typed, or generated by the TD numbering scheme in force ({YEAR} = the
        // effectivity year); docs/FORMS-REVISION-PLAN.md section 4.4.
        var today = clock.Today;
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        // {GRYEAR}/{REV}: the general revision in force on the TD's effectivity (identification-numbering.md §4.1).
        var context = await NumberContexts.ForPropertyAsync(db, rpu.PropertyId, request.EffectivityDate.Year, cancellationToken, request.EffectivityDate);
        var assigned = await numbering.AssignNumberAsync(NumberedDocumentKind.TaxDeclaration, context, request.TaxDeclarationNumber, today, cancellationToken);
        if (assigned.IsFailure)
        {
            return Result.Failure<TaxDeclarationDto>(assigned.Code!, assigned.Message!);
        }
        var number = Result.Success(assigned.Value.Value);
        // TD numbers are unique across the province, whoever's jurisdiction holds them.
        if (await db.TaxDeclarations.IgnoreQueryFilters().AnyAsync(td => td.TaxDeclarationNumber == number.Value, cancellationToken))
        {
            return Result.Failure<TaxDeclarationDto>("TAX_DECLARATION_NUMBER_DUPLICATE", $"A Tax Declaration with number '{number.Value}' already exists.");
        }

        var taxDeclaration = new TaxDeclaration
        {
            RpuId = request.RpuId,
            PropertyId = rpu.PropertyId,
            TaxDeclarationNumber = number.Value,
            AssessmentCount = assigned.Value.Sequence,
            RevisionNumber = revisionNumber,
            EffectivityDate = request.EffectivityDate,
            // Declaring an assessment, it is as taxable as its lines (assessment-listing-exemptions.md Q2).
            Taxability = assessmentId is { } declaredId && await ExemptionTaxability.OfAssessmentAsync(db, declaredId, cancellationToken) is { } derived
                ? derived : request.Taxability,
            ClassificationId = request.ClassificationId,
            ActualUseId = request.ActualUseId,
            SubClassificationId = request.SubClassificationId,
            AssessmentYear = request.AssessmentYear,
            PreviousTaxDeclarationId = request.PreviousTaxDeclarationId,
            Remarks = request.Remarks,
            PropertyTransactionId = request.PropertyTransactionId,
            AssessmentId = assessmentId,
            RestoresTaxDeclarationId = request.RestoresTaxDeclarationId,
            TransactionCode = transactionCode,
            TransactionRank = transactionRank,
            Status = WorkflowStatus.Draft,
        };

        db.TaxDeclarations.Add(taxDeclaration);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Result.Success(await MapToDto(taxDeclaration.Id, cancellationToken)
            ?? throw new InvalidOperationException("Tax Declaration was just created but could not be reloaded."));
    }

    public async Task<Result<TaxDeclarationDto>> GetByIdAsync(Guid taxDeclarationId, CancellationToken cancellationToken = default)
    {
        var dto = await MapToDto(taxDeclarationId, cancellationToken);
        return dto is null
            ? Result.Failure<TaxDeclarationDto>("TAX_DECLARATION_NOT_FOUND", "No Tax Declaration was found with the given id.")
            : Result.Success(dto);
    }

    public async Task<Result<IReadOnlyList<TaxDeclarationDto>>> ListByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default)
    {
        var entities = await IncludeReferences(db.TaxDeclarations)
            .Where(td => td.RpuId == rpuId)
            .OrderByDescending(td => td.RevisionNumber)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<TaxDeclarationDto>>(entities.Select(ProjectToDto).ToList());
    }

    // .Include() required — see the identical comment in PropertyService.MapToDto.
    private async Task<TaxDeclarationDto?> MapToDto(Guid id, CancellationToken cancellationToken)
    {
        var entity = await IncludeReferences(db.TaxDeclarations).SingleOrDefaultAsync(td => td.Id == id, cancellationToken);
        return entity is null ? null : ProjectToDto(entity);
    }

    public async Task<Result<TaxDeclarationDto>> SubmitForReviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var td = await db.TaxDeclarations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (td is null)
        {
            return NotFound();
        }
        if (td.PropertyTransactionId is not null)
        {
            return InTransaction();
        }
        if (td.Status != WorkflowStatus.Draft)
        {
            return Result.Failure<TaxDeclarationDto>("TAX_DECLARATION_NOT_DRAFT", "Only a Draft Tax Declaration can be submitted for review.");
        }
        td.Status = WorkflowStatus.PendingReview;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success((await MapToDto(id, cancellationToken))!);
    }

    public async Task<Result<TaxDeclarationDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var td = await db.TaxDeclarations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (td is null)
        {
            return NotFound();
        }
        if (td.PropertyTransactionId is not null)
        {
            return InTransaction();
        }
        if (td.Status != WorkflowStatus.PendingReview)
        {
            return Result.Failure<TaxDeclarationDto>("TAX_DECLARATION_NOT_PENDING_REVIEW", "Only a Tax Declaration pending review can be approved.");
        }

        var step = await approvals.SignNextStepAsync(ApprovalSubjectType.TaxDeclaration, td.Id, td.CreatedBy, clock.Today, null, cancellationToken);
        if (step.IsFailure)
        {
            return Result.Failure<TaxDeclarationDto>(step.Code!, step.Message!);
        }
        if (!step.Value.ChainInForce && currentUser.AppUserId is not null && td.CreatedBy == currentUser.AppUserId)
        {
            return Result.Failure<TaxDeclarationDto>("CANNOT_APPROVE_OWN_TAX_DECLARATION", "The Tax Declaration's creator cannot also approve it (CLAUDE.md §46).");
        }
        var completes = !step.Value.ChainInForce || step.Value.Completed;

        TaxDeclaration? previous = null;
        if (completes)
        {
            var check = await TaxDeclarationApproval.CheckAsync(db, td, faas.Value.RequireAssessmentOnTd, cancellationToken);
            if (check.IsFailure)
            {
                return Result.Failure<TaxDeclarationDto>(check.Code!, check.Message!);
            }
            previous = check.Value;
        }

        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            if (completes)
            {
                await TaxDeclarationApproval.ApplyAsync(db, td, previous, currentUser.AppUserId, clock.UtcNow, clock.Today, cancellationToken);
            }
            else
            {
                await db.SaveChangesAsync(cancellationToken); // the signed step
            }
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException)
        {
            return Result.Failure<TaxDeclarationDto>("TAX_DECLARATION_APPROVAL_CONFLICT",
                "Another approval for this RPU happened at the same time. Nothing was changed; reload and try again.");
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
        if (completes)
        {
            // Approval is the submission to the province: freeze the printed TD and FAAS (§3.7, LP-6).
            await issuer.IssueAsync([td.Id], cancellationToken);
        }
        return Result.Success((await MapToDto(id, cancellationToken))!);
    }

    public async Task<Result<TaxDeclarationDto>> RejectAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
        {
            return Result.Failure<TaxDeclarationDto>("VALIDATION_FAILED", "A reason is required (max 1000).");
        }
        var td = await db.TaxDeclarations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (td is null)
        {
            return NotFound();
        }
        if (td.PropertyTransactionId is not null)
        {
            return InTransaction();
        }
        if (td.Status != WorkflowStatus.PendingReview)
        {
            return Result.Failure<TaxDeclarationDto>("TAX_DECLARATION_NOT_PENDING_REVIEW", "Only a Tax Declaration pending review can be rejected.");
        }
        td.Status = WorkflowStatus.Rejected;
        td.CancellationReason = reason;
        currentUser.Reason = reason;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success((await MapToDto(id, cancellationToken))!);
    }

    public async Task<Result<TaxDeclarationDto>> CancelAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
        {
            return Result.Failure<TaxDeclarationDto>("VALIDATION_FAILED", "A reason is required (max 1000).");
        }
        var td = await db.TaxDeclarations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (td is null)
        {
            return NotFound();
        }
        if (td.Status != WorkflowStatus.Approved)
        {
            return Result.Failure<TaxDeclarationDto>("TAX_DECLARATION_NOT_APPROVED",
                "Only an approved Tax Declaration can be cancelled; a draft or pending one can be rejected instead.");
        }
        if (await CancellationGuard.BlockerAsync(db, [td.Id], cancellationToken) is { } blocked)
        {
            return Result.Failure<TaxDeclarationDto>(CancellationGuard.Code, blocked);
        }
        td.Status = WorkflowStatus.Cancelled;
        td.CancelledAt = clock.UtcNow;
        td.CancelledBy = currentUser.AppUserId;
        td.CancellationReason = reason;
        currentUser.Reason = reason;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success((await MapToDto(id, cancellationToken))!);
    }

    public async Task<Result<IReadOnlyList<TaxDeclarationAnnotationDto>>> ListAnnotationsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await db.TaxDeclarations.AnyAsync(x => x.Id == id, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<TaxDeclarationAnnotationDto>>("TAX_DECLARATION_NOT_FOUND", "No Tax Declaration was found with the given id.");
        }
        var rows = await db.TaxDeclarationAnnotations.Include(x => x.AnnotationType)
            .Where(x => x.TaxDeclarationId == id)
            .OrderBy(x => x.LiftedAt != null).ThenByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var sources = await CarriedFromNumbersAsync(rows, cancellationToken);
        return Result.Success<IReadOnlyList<TaxDeclarationAnnotationDto>>(rows.Select(a => ToDto(a, sources)).ToList());
    }

    public async Task<Result<TaxDeclarationAnnotationDto>> AddAnnotationAsync(Guid id, AddTaxDeclarationAnnotationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 2000
            || request.ReferenceNumber?.Length > 100 || request.EffectiveDate == default)
        {
            return Result.Failure<TaxDeclarationAnnotationDto>("VALIDATION_FAILED",
                "text is required (max 2000), referenceNumber max 100, effectiveDate is required.");
        }
        var td = await db.TaxDeclarations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (td is null)
        {
            return Result.Failure<TaxDeclarationAnnotationDto>("TAX_DECLARATION_NOT_FOUND", "No Tax Declaration was found with the given id.");
        }
        if (td.Status is WorkflowStatus.Cancelled or WorkflowStatus.Voided or WorkflowStatus.Rejected)
        {
            return Result.Failure<TaxDeclarationAnnotationDto>("TAX_DECLARATION_NOT_ANNOTATABLE", $"A {td.Status} Tax Declaration cannot be annotated.");
        }
        if (!await db.AnnotationTypes.AnyAsync(x => x.Id == request.AnnotationTypeId && x.IsActive, cancellationToken))
        {
            return Result.Failure<TaxDeclarationAnnotationDto>("ANNOTATION_TYPE_NOT_FOUND", "The specified annotation type does not exist or is inactive.");
        }
        var annotation = new TaxDeclarationAnnotation
        {
            TaxDeclarationId = id, AnnotationTypeId = request.AnnotationTypeId, Text = request.Text.Trim(),
            ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim(),
            ReferenceDate = request.ReferenceDate, EffectiveDate = request.EffectiveDate,
        };
        db.TaxDeclarationAnnotations.Add(annotation);
        await db.SaveChangesAsync(cancellationToken);
        await db.TaxDeclarationAnnotations.Entry(annotation).Reference(x => x.AnnotationType).LoadAsync(cancellationToken);
        return Result.Success(ToDto(annotation));
    }

    public async Task<Result<TaxDeclarationAnnotationDto>> LiftAnnotationAsync(Guid annotationId, LiftTaxDeclarationAnnotationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000 || request.Reference?.Length > 100)
        {
            return Result.Failure<TaxDeclarationAnnotationDto>("VALIDATION_FAILED", "A reason is required (max 1000); reference max 100.");
        }
        var annotation = await db.TaxDeclarationAnnotations.Include(x => x.AnnotationType).FirstOrDefaultAsync(x => x.Id == annotationId, cancellationToken);
        if (annotation is null)
        {
            return Result.Failure<TaxDeclarationAnnotationDto>("ANNOTATION_NOT_FOUND", "No annotation was found with the given id.");
        }
        if (annotation.LiftedAt is not null)
        {
            return Result.Failure<TaxDeclarationAnnotationDto>("ANNOTATION_ALREADY_LIFTED", "This annotation has already been lifted.");
        }
        annotation.LiftedAt = clock.UtcNow;
        annotation.LiftedBy = currentUser.AppUserId;
        annotation.LiftReason = request.Reason;
        annotation.LiftReference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();
        currentUser.Reason = request.Reason;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(annotation, await CarriedFromNumbersAsync([annotation], cancellationToken)));
    }

    private static Result<TaxDeclarationDto> InTransaction() =>
        Result.Failure<TaxDeclarationDto>("TAX_DECLARATION_IN_TRANSACTION",
            "This Tax Declaration belongs to a property transaction; it is submitted and approved with the transaction.");

    private static Result<TaxDeclarationDto> NotFound() =>
        Result.Failure<TaxDeclarationDto>("TAX_DECLARATION_NOT_FOUND", "No Tax Declaration was found with the given id.");

    /// <summary>The TD number each carried annotation was copied from, by source annotation.</summary>
    private async Task<Dictionary<Guid, string>> CarriedFromNumbersAsync(IReadOnlyCollection<TaxDeclarationAnnotation> rows, CancellationToken ct)
    {
        var ids = rows.Select(x => x.CarriedFromAnnotationId).OfType<Guid>().ToList();
        return ids.Count == 0 ? [] : await db.TaxDeclarationAnnotations.Where(x => ids.Contains(x.Id))
            .Join(db.TaxDeclarations, a => a.TaxDeclarationId, t => t.Id, (a, t) => new { a.Id, t.TaxDeclarationNumber })
            .ToDictionaryAsync(x => x.Id, x => x.TaxDeclarationNumber, ct);
    }

    private static TaxDeclarationAnnotationDto ToDto(TaxDeclarationAnnotation a, IReadOnlyDictionary<Guid, string>? carriedFrom = null) => new(
        a.Id, a.TaxDeclarationId, a.AnnotationTypeId, a.AnnotationType!.Code, a.AnnotationType.Name, a.Text, a.ReferenceNumber,
        a.ReferenceDate, a.EffectiveDate, a.CreatedAt, a.CreatedBy, a.LiftedAt, a.LiftedBy, a.LiftReason, a.LiftReference,
        a.CarriedFromAnnotationId, a.CarriedFromAnnotationId is { } source ? carriedFrom?.GetValueOrDefault(source) : null);

    private static IQueryable<TaxDeclaration> IncludeReferences(IQueryable<TaxDeclaration> query) => query
        .Include(td => td.Classification)
        .Include(td => td.ActualUse)
        .Include(td => td.Annotations)
        .Include(td => td.Assessment);

    private TaxDeclarationDto ProjectToDto(TaxDeclaration td) => new(
        td.Id,
        td.RpuId,
        td.PropertyId,
        td.TaxDeclarationNumber,
        td.RevisionNumber,
        td.EffectivityDate,
        td.Taxability,
        td.ClassificationId,
        td.Classification!.Name,
        td.ActualUseId,
        td.ActualUse!.Name,
        td.SubClassificationId,
        td.AssessmentYear,
        td.Status,
        td.PreviousTaxDeclarationId,
        td.Remarks,
        td.CreatedAt,
        td.CreatedBy,
        td.ApprovedBy,
        td.ApprovedAt,
        td.CancelledAt,
        td.CancellationReason,
        td.SupersededByTaxDeclarationId,
        td.Annotations.Count(a => a.LiftedAt == null),
        td.PropertyTransactionId,
        td.AssessmentId,
        FaasNumber(td),
        td.TransactionCode,
        td.TransactionRank,
        td.AssessmentCount,
        td.RestoresTaxDeclarationId);

    /// <summary>A TD is a FAAS once it declares an assessment; its number follows <see cref="FaasOptions.NumberSource"/>.</summary>
    private string? FaasNumber(TaxDeclaration td) => td.AssessmentId is null
        ? null
        : faas.Value.NumberSource == FaasNumberSource.TaxDeclaration ? td.TaxDeclarationNumber : td.Assessment?.FaasNumber;
}
