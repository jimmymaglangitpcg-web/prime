using Prime.Domain.Enums;

namespace Prime.Application.Features.Assessments;

/// <param name="AssessmentYear">Null: the effectivity year.</param>
/// <param name="EffectiveDate">
/// Required when no effectivity rule gives the date (no transaction type, or a type with a
/// <c>Periods</c>/<c>Fixed</c> rule, or a general revision). With a rule that derives the
/// date, it is left out, or differs from the derived date only with
/// <paramref name="EffectivityOverrideReason"/> (docs/analysis/valuation-foundation.md §4.2).
/// </param>
/// <param name="TransactionTypeId">A transaction type in force; its rule decides the effectivity.</param>
/// <param name="CauseDate">The event a reassessment answers; required by the <c>NextQuarter</c> rule.</param>
public sealed record CreateAssessmentRequest(
    Guid ValuationId,
    int? AssessmentYear,
    DateOnly? EffectiveDate,
    Guid? PreviousAssessmentId,
    Guid? RevisionReference,
    string? Remarks,
    Guid? TransactionTypeId = null,
    DateOnly? CauseDate = null,
    string? EffectivityOverrideReason = null);

/// <summary>The effectivity an assessment would take if made (finally approved) today.</summary>
public sealed record EffectivityDto(
    DateOnly EffectiveDate,
    int Year,
    int Quarter,
    EffectivityRule? Rule,
    bool Derived,
    bool Overridden,
    Guid? TransactionTypeId,
    string? TransactionCode,
    string? LegalBasis,
    DateOnly MadeOn,
    DateOnly? CauseDate,
    int? CauseWindowDays,
    bool CauseWindowExceeded);

public sealed record AssessmentDto(
    Guid Id,
    Guid RpuId,
    Guid PropertyId,
    Guid ValuationId,
    int AssessmentYear,
    decimal MarketValue,
    Guid? AssessmentLevelId,
    decimal? AssessmentPercentage,
    decimal AssessedValue,
    WorkflowStatus Status,
    DateOnly EffectiveDate,
    Guid? PreviousAssessmentId,
    Guid? RevisionReference,
    string? Remarks,
    string? FaasNumber,
    Guid? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AssessmentLineDto> Lines,
    DateTimeOffset? PostedAt = null,
    Guid? PostedBy = null,
    /// <summary>Set by posting only: whether a draft Tax Declaration was prepared, and if not, why.</summary>
    string? TaxDeclarationNote = null,
    int EffectivityYear = 0,
    int EffectivityQuarter = 0,
    Guid? TransactionTypeId = null,
    string? TransactionCode = null,
    EffectivityRule? EffectivityRule = null,
    DateOnly? CauseDate = null,
    int? CauseWindowDays = null,
    bool CauseWindowExceeded = false,
    DateOnly? MadeOn = null,
    string? EffectivityOverrideReason = null);

/// <summary>A FAAS "Property Assessment" row (docs/analysis/mrpaao-forms-model.md §8.2).</summary>
public sealed record AssessmentLineDto(
    Guid Id, int Sequence, Guid ClassificationId, string ClassificationName, Guid ActualUseId, string ActualUseName,
    decimal MarketValue, Guid AssessmentLevelId, decimal AssessmentPercentage, decimal AssessedValue,
    Taxability Taxability = Taxability.Taxable, Guid? PropertyExemptionId = null, string? TaxabilityNote = null, string? ExemptionLegalBasis = null);

/// <summary>What an assessment of a valuation would be, not saved (docs/analysis/value-and-assess.md §2.3).</summary>
public sealed record AssessmentPreviewDto(Guid ValuationId, Guid RpuId, decimal MarketValue, decimal AssessedValue, IReadOnlyList<AssessmentLineDto> Lines,
    EffectivityDto? Effectivity = null);
