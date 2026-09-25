using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Forms;

public sealed record CreateFormDefinitionRequest(
    string LegalBasis, DateOnly EffectiveDate, string? Remarks,
    string Code, string Title, FormSubjectType SubjectType, FormAuthority Authority, string? SourceReference, string TemplateBody);

public sealed record FormDefinitionDto(
    Guid Id, string Code, int Version, string Title, FormSubjectType SubjectType, FormAuthority Authority, string? SourceReference,
    string LegalBasis, DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status,
    Guid? CreatedBy, DateTimeOffset CreatedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, string? Remarks,
    string? TemplateBody);

public sealed record IssueFormRequest(string FormCode, Guid SubjectId);

public sealed record CancelIssuedFormRequest(string Reason);

public sealed record FormPreviewDto(string FormCode, int FormVersion, string Title, FormAuthority Authority, string? IssueBlocker, string Html);

public sealed record IssuedFormDto(
    Guid Id, string FormCode, int FormVersion, string Title, FormAuthority Authority, FormSubjectType SubjectType, Guid SubjectId,
    string? DocumentNumber, WorkflowStatus Status, DateTimeOffset IssuedAt, Guid? IssuedBy,
    DateTimeOffset? CancelledAt, string? CancellationReason, string RenderedHtmlSha256, string? Html);

public sealed class CreateFormDefinitionRequestValidator : AbstractValidator<CreateFormDefinitionRequest>
{
    public CreateFormDefinitionRequestValidator(IFormRenderer renderer)
    {
        RuleFor(x => x.LegalBasis).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("effectiveDate is required.");
        RuleFor(x => x.Remarks).MaximumLength(1000);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).Matches("^[A-Z][A-Z0-9_]*$").WithMessage("code must be UPPER_SNAKE_CASE.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SubjectType).IsInEnum();
        RuleFor(x => x.Authority).IsInEnum();
        RuleFor(x => x.SourceReference).MaximumLength(500);
        RuleFor(x => x.TemplateBody).NotEmpty()
            .Must(body => renderer.Validate(body) is null).WithMessage(x => $"templateBody: {renderer.Validate(x.TemplateBody)}");
    }
}

public interface IFormService
{
    Task<Result<FormDefinitionDto>> CreateDefinitionAsync(CreateFormDefinitionRequest request, CancellationToken cancellationToken = default);
    Task<Result<FormDefinitionDto>> ApproveDefinitionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<FormDefinitionDto>> GetDefinitionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<FormDefinitionDto>>> ListDefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Renders the form in force for <paramref name="subjectId"/> without issuing or saving anything.</summary>
    Task<Result<FormPreviewDto>> PreviewAsync(string formCode, Guid subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues the form in force: freezes the data snapshot and rendered HTML.
    /// Idempotent — a subject already issued under this form version gets the
    /// existing issue back; to reflect changed data, cancel it and issue again.
    /// </summary>
    Task<Result<IssuedFormDto>> IssueAsync(IssueFormRequest request, CancellationToken cancellationToken = default);
    Task<Result<IssuedFormDto>> GetIssuedAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<IssuedFormDto>>> ListIssuedAsync(Guid subjectId, CancellationToken cancellationToken = default);
    Task<Result<IssuedFormDto>> CancelIssuedAsync(Guid id, string reason, CancellationToken cancellationToken = default);
}

/// <summary>docs/FORMS-REVISION-PLAN.md §4.1–§4.3.</summary>
public sealed class FormService(
    IApplicationDbContext db,
    IValidator<CreateFormDefinitionRequest> validator,
    ICurrentUserService currentUser,
    IFormRenderer renderer,
    IEnumerable<IFormDataProvider> providers,
    IOptions<LguOptions> lgu,
    IClock clock) : IFormService
{
    public async Task<Result<FormDefinitionDto>> CreateDefinitionAsync(CreateFormDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<FormDefinitionDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        var existing = await db.FormDefinitions.Where(x => x.Code == request.Code).ToListAsync(cancellationToken);
        if (existing.FirstOrDefault(x => x.SubjectType != request.SubjectType) is { } other)
        {
            return Result.Failure<FormDefinitionDto>("FORM_SUBJECT_MISMATCH",
                $"Form {request.Code} renders {other.SubjectType}; a new version must render the same kind of record.");
        }
        var definition = new FormDefinition
        {
            LegalBasis = request.LegalBasis, EffectiveDate = request.EffectiveDate, Remarks = request.Remarks,
            Code = request.Code, Version = existing.Count == 0 ? 1 : existing.Max(x => x.Version) + 1,
            Title = request.Title, SubjectType = request.SubjectType, Authority = request.Authority,
            SourceReference = request.SourceReference, TemplateBody = request.TemplateBody,
        };
        db.FormDefinitions.Add(definition);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<FormDefinitionDto>("FORM_VERSION_CONFLICT", "Another version of this form was created at the same time. Try again.");
        }
        return Result.Success(ToDto(definition, includeTemplate: true));
    }

    public async Task<Result<FormDefinitionDto>> ApproveDefinitionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await db.FormDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (definition is null)
        {
            return DefinitionNotFound();
        }
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, db.FormDefinitions.Where(x => x.Code == definition.Code),
                definition, "FORM_DEFINITION", cancellationToken) is { } failure)
        {
            return Result.Failure<FormDefinitionDto>(failure.Code!, failure.Message!);
        }
        return Result.Success(ToDto(definition, includeTemplate: true));
    }

    public async Task<Result<FormDefinitionDto>> GetDefinitionAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.FormDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) is { } definition
            ? Result.Success(ToDto(definition, includeTemplate: true))
            : DefinitionNotFound();

    public async Task<Result<IReadOnlyList<FormDefinitionDto>>> ListDefinitionsAsync(CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<FormDefinitionDto>>((await db.FormDefinitions
            .OrderBy(x => x.Code).ThenByDescending(x => x.Version).ToListAsync(cancellationToken))
            .Select(x => ToDto(x, includeTemplate: false)).ToList());

    public async Task<Result<FormPreviewDto>> PreviewAsync(string formCode, Guid subjectId, CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(formCode, subjectId, preview: true, cancellationToken);
        if (prepared.IsFailure)
        {
            return Result.Failure<FormPreviewDto>(prepared.Code!, prepared.Message!);
        }
        var (definition, subject, snapshot) = prepared.Value;
        var html = renderer.Render(definition.TemplateBody, snapshot, Provisional(definition));
        return Result.Success(new FormPreviewDto(definition.Code, definition.Version, definition.Title, definition.Authority, subject.IssueBlocker, html));
    }

    public async Task<Result<IssuedFormDto>> IssueAsync(IssueFormRequest request, CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(request.FormCode, request.SubjectId, preview: false, cancellationToken);
        if (prepared.IsFailure)
        {
            return Result.Failure<IssuedFormDto>(prepared.Code!, prepared.Message!);
        }
        var (definition, subject, snapshot) = prepared.Value;
        if (subject.IssueBlocker is { } blocker)
        {
            return Result.Failure<IssuedFormDto>("FORM_SUBJECT_NOT_ISSUABLE", blocker);
        }
        if (await ValidIssueAsync(definition.Id, request.SubjectId, cancellationToken) is { } existing)
        {
            return Result.Success(ToDto(existing, definition.Title, includeHtml: true));
        }

        var html = renderer.Render(definition.TemplateBody, snapshot, Provisional(definition));
        var issued = new IssuedForm
        {
            FormDefinitionId = definition.Id, FormCode = definition.Code, FormVersion = definition.Version, Authority = definition.Authority,
            SubjectType = definition.SubjectType, SubjectId = request.SubjectId, DocumentNumber = subject.DocumentNumber,
            DataSnapshotJson = snapshot.ToJsonString(), RenderedHtml = html,
            RenderedHtmlSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(html))),
            Status = WorkflowStatus.Posted, IssuedAt = snapshot["issue"]!["issuedAt"]!.GetValue<DateTimeOffset>(), IssuedBy = currentUser.AppUserId,
        };
        db.IssuedForms.Add(issued);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // UX_IssuedForms_Definition_Subject_Valid: issued concurrently — return that one.
            db.IssuedForms.Remove(issued);
            if (await ValidIssueAsync(definition.Id, request.SubjectId, cancellationToken) is { } raced)
            {
                return Result.Success(ToDto(raced, definition.Title, includeHtml: true));
            }
            throw;
        }
        return Result.Success(ToDto(issued, definition.Title, includeHtml: true));
    }

    public async Task<Result<IssuedFormDto>> GetIssuedAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.IssuedForms.Include(x => x.FormDefinition).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken) is { } issued
            ? Result.Success(ToDto(issued, issued.FormDefinition!.Title, includeHtml: true))
            : IssuedNotFound();

    public async Task<Result<IReadOnlyList<IssuedFormDto>>> ListIssuedAsync(Guid subjectId, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<IssuedFormDto>>((await db.IssuedForms.Include(x => x.FormDefinition).AsNoTracking()
            .Where(x => x.SubjectId == subjectId).OrderByDescending(x => x.IssuedAt).ToListAsync(cancellationToken))
            .Select(x => ToDto(x, x.FormDefinition!.Title, includeHtml: false)).ToList());

    public async Task<Result<IssuedFormDto>> CancelIssuedAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
        {
            return Result.Failure<IssuedFormDto>("VALIDATION_FAILED", "A cancellation reason is required (max 1000).");
        }
        var issued = await db.IssuedForms.Include(x => x.FormDefinition).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (issued is null)
        {
            return IssuedNotFound();
        }
        if (issued.Status == WorkflowStatus.Cancelled)
        {
            return Result.Failure<IssuedFormDto>("ISSUED_FORM_ALREADY_CANCELLED", "This issued form is already cancelled.");
        }
        issued.Status = WorkflowStatus.Cancelled;
        issued.CancelledAt = DateTimeOffset.UtcNow;
        issued.CancelledBy = currentUser.AppUserId;
        issued.CancellationReason = reason;
        currentUser.Reason = reason;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(issued, issued.FormDefinition!.Title, includeHtml: false));
    }

    /// <summary>The form version in force today, the subject's data, and the full snapshot (subject data + form, LGU and issue metadata).</summary>
    private async Task<Result<(FormDefinition Definition, FormSubjectData Subject, JsonObject Snapshot)>> PrepareAsync(
        string formCode, Guid subjectId, bool preview, CancellationToken ct)
    {
        var today = clock.Today;
        var definition = await db.FormDefinitions.InForce(today).FirstOrDefaultAsync(x => x.Code == formCode, ct);
        if (definition is null)
        {
            return Result.Failure<(FormDefinition, FormSubjectData, JsonObject)>("FORM_NOT_CONFIGURED", $"No approved version of form {formCode} is in force.");
        }
        var provider = providers.SingleOrDefault(p => p.SubjectType == definition.SubjectType)
            ?? throw new InvalidOperationException($"No form data provider for {definition.SubjectType}.");
        var subject = await provider.BuildAsync(subjectId, ct);
        if (subject is null)
        {
            return Result.Failure<(FormDefinition, FormSubjectData, JsonObject)>("FORM_SUBJECT_NOT_FOUND", $"No {definition.SubjectType} was found with the given id.");
        }

        var issuerName = currentUser.AppUserId is { } userId
            ? await db.AppUsers.Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct)
            : null;
        var snapshot = (JsonObject)subject.Data.DeepClone();
        snapshot["form"] = FormData.ToJson(new
        {
            code = definition.Code, version = definition.Version, title = definition.Title,
            authority = definition.Authority.ToString(), provisional = Provisional(definition),
            sourceReference = definition.SourceReference, legalBasis = definition.LegalBasis,
        });
        snapshot["lgu"] = FormData.ToJson(new { name = lgu.Value.Name, office = lgu.Value.Office, province = lgu.Value.Province, address = lgu.Value.Address,
            sanggunianName = lgu.Value.SanggunianName });
        snapshot["issue"] = FormData.ToJson(new { issuedAt = clock.UtcNow, issuedBy = issuerName, isPreview = preview, documentNumber = subject.DocumentNumber });
        return Result.Success((definition, subject, snapshot));
    }

    private Task<IssuedForm?> ValidIssueAsync(Guid definitionId, Guid subjectId, CancellationToken ct) =>
        db.IssuedForms.AsNoTracking().FirstOrDefaultAsync(x => x.FormDefinitionId == definitionId && x.SubjectId == subjectId && x.Status == WorkflowStatus.Posted, ct);

    private static bool Provisional(FormDefinition d) => d.Authority == FormAuthority.PrimeProvisional;

    private static Result<FormDefinitionDto> DefinitionNotFound() =>
        Result.Failure<FormDefinitionDto>("FORM_DEFINITION_NOT_FOUND", "No form definition was found with the given id.");

    private static Result<IssuedFormDto> IssuedNotFound() =>
        Result.Failure<IssuedFormDto>("ISSUED_FORM_NOT_FOUND", "No issued form was found with the given id.");

    private static FormDefinitionDto ToDto(FormDefinition x, bool includeTemplate) => new(
        x.Id, x.Code, x.Version, x.Title, x.SubjectType, x.Authority, x.SourceReference, x.LegalBasis, x.EffectiveDate, x.EndDate,
        x.Status, x.CreatedBy, x.CreatedAt, x.ApprovedBy, x.ApprovedAt, x.Remarks, includeTemplate ? x.TemplateBody : null);

    private static IssuedFormDto ToDto(IssuedForm x, string title, bool includeHtml) => new(
        x.Id, x.FormCode, x.FormVersion, title, x.Authority, x.SubjectType, x.SubjectId, x.DocumentNumber, x.Status,
        x.IssuedAt, x.IssuedBy, x.CancelledAt, x.CancellationReason, x.RenderedHtmlSha256, includeHtml ? x.RenderedHtml : null);
}
