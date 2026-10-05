using Prime.Domain.Enums;

namespace Prime.Application.Features.Smv;

/// <param name="MunicipalityIds">The municipalities covered; none = the whole province (docs/analysis/valuation-foundation.md §4.3, Q2).</param>
public sealed record CreateSmvRequest(
    string? OrdinanceNumber,
    DateOnly? OrdinanceDate,
    DateOnly? ApprovalDate,
    DateOnly EffectivityDate,
    int RevisionYear,
    string? Description,
    SmvBasis Basis = SmvBasis.Ordinance,
    DateOnly? ProposedOn = null,
    DateOnly? PublishedForCommentOn = null,
    DateOnly? ConsultationsHeldOn = null,
    DateOnly? SubmittedToBlgfOn = null,
    DateOnly? CertifiedOn = null,
    string? CertificationReference = null,
    DateOnly? PublishedOn = null,
    string? PublicationReference = null,
    IReadOnlyList<Guid>? MunicipalityIds = null,
    /// <summary>An <see cref="SmvBasis.Amendment"/>: the approved SMV it amends (§4.7).</summary>
    Guid? AmendsSmvId = null,
    SmvAmendmentGround? AmendmentGround = null);

public sealed record SmvCoverageDto(Guid MunicipalityId, string MunicipalityName);

public sealed record SmvDto(
    Guid Id,
    string? OrdinanceNumber,
    DateOnly? OrdinanceDate,
    DateOnly? ApprovalDate,
    DateOnly EffectivityDate,
    int RevisionYear,
    WorkflowStatus Status,
    string? Description,
    DateTimeOffset CreatedAt,
    SmvBasis Basis = SmvBasis.Ordinance,
    string Reference = "",
    DateOnly? ProposedOn = null,
    DateOnly? PublishedForCommentOn = null,
    DateOnly? ConsultationsHeldOn = null,
    DateOnly? SubmittedToBlgfOn = null,
    DateOnly? CertifiedOn = null,
    string? CertificationReference = null,
    DateOnly? PublishedOn = null,
    string? PublicationReference = null,
    /// <summary>Empty: the whole province.</summary>
    IReadOnlyList<SmvCoverageDto>? Coverage = null,
    Guid? AmendsSmvId = null,
    string? AmendsSmvReference = null,
    SmvAmendmentGround? AmendmentGround = null);

/// <param name="ActualUseId">Optional: a rate naming the actual use beats one that does not (§4.3).</param>
/// <param name="SubClassificationId">The sub-class the unit value is for; null: any.</param>
/// <param name="BarangayId">A unit value given for one barangay; null: any.</param>
public sealed record CreateSmvScheduleRequest(
    Guid ClassificationId,
    Guid? ActualUseId,
    Guid PropertyTypeId,
    Guid? ZoneId,
    string Unit,
    decimal MarketValue,
    decimal? MinimumValue,
    decimal? MaximumValue,
    DateOnly EffectiveDate,
    Guid? ImprovementKindId = null,
    Guid? SubClassificationId = null,
    Guid? BarangayId = null,
    /// <summary>Printed on SMV Forms 5 and 9 (docs/analysis/smv-preparation-general-revision.md §4.2); never used to select a rate.</summary>
    string? LocationDescription = null,
    string? CropDescription = null);

public sealed record SmvScheduleDto(
    Guid Id,
    Guid SmvId,
    Guid ClassificationId,
    string ClassificationName,
    Guid? ActualUseId,
    string? ActualUseName,
    Guid PropertyTypeId,
    string PropertyTypeName,
    Guid? ZoneId,
    string? ZoneName,
    Guid? ImprovementKindId,
    string? ImprovementKindName,
    string Unit,
    decimal MarketValue,
    decimal? MinimumValue,
    decimal? MaximumValue,
    DateOnly EffectiveDate,
    DateOnly? EndDate,
    WorkflowStatus Status,
    DateTimeOffset CreatedAt,
    Guid? SubClassificationId = null,
    string? SubClassificationName = null,
    Guid? BarangayId = null,
    string? BarangayName = null,
    string? LocationDescription = null,
    string? CropDescription = null,
    /// <summary>An amendment's row: the amended SMV's value for the same key, which it replaces; null when it adds a row.</summary>
    decimal? ReplacesMarketValue = null);
