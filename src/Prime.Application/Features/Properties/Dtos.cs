using Prime.Domain.Enums;

namespace Prime.Application.Features.Properties;

public sealed record CreatePropertyRequest(
    string? PropertyIdentificationNumber,
    Guid ProvinceId,
    Guid MunicipalityId,
    Guid BarangayId,
    Guid? ZoneId,
    string? Street,
    string? Sitio,
    string? LotNumber,
    string? BlockNumber,
    string? SurveyNumber,
    string? TitleNumber,
    string? TaxMapNumber);

public sealed record PropertyDto(
    Guid Id,
    string PropertyIdentificationNumber,
    Guid ProvinceId,
    string ProvinceName,
    Guid MunicipalityId,
    string MunicipalityName,
    Guid BarangayId,
    string BarangayName,
    Guid? ZoneId,
    string? ZoneName,
    string? Street,
    string? Sitio,
    string? LotNumber,
    string? BlockNumber,
    string? SurveyNumber,
    string? TitleNumber,
    string? TaxMapNumber,
    RecordStatus Status,
    DateTimeOffset CreatedAt);

/// <summary>
/// CLAUDE.md §50 Property Profile — sections that have real data by Phase
/// 4 (GIS map, current assessment, billing, payments, delinquency are
/// omitted; they render as "not yet available" in the UI once those
/// phases land, not faked here).
/// </summary>
public sealed record PropertyProfileDto(
    PropertyDto Property,
    IReadOnlyList<PropertyOwnerDto> Owners,
    IReadOnlyList<ParcelSummaryDto> Parcels,
    IReadOnlyList<RpuSummaryDto> Rpus,
    IReadOnlyList<TaxDeclarationSummaryDto> TaxDeclarations);

/// <summary>A party to the property (LGC §§204–205). TaxpayerId is null only for an unknown owner.</summary>
public sealed record PropertyOwnerDto(
    Guid PropertyTaxpayerId,
    Guid? TaxpayerId,
    string TaxpayerDisplayName,
    string? OwnershipTypeName,
    decimal OwnershipPercentage,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsCurrent,
    PropertyPartyRole Role,
    string? EndReason,
    string? Address);

public sealed record ParcelSummaryDto(
    Guid Id,
    decimal? Area,
    string? LotNumber,
    string BarangayName,
    RecordStatus Status);

public sealed record RpuSummaryDto(
    Guid Id,
    string RpuNumber,
    RpuType RpuType,
    RecordStatus Status,
    DateOnly EffectivityDate);

public sealed record TaxDeclarationSummaryDto(
    Guid Id,
    string TaxDeclarationNumber,
    int RevisionNumber,
    int AssessmentYear,
    WorkflowStatus Status,
    DateOnly EffectivityDate);

public sealed class PropertySearchRequest : Common.PagedRequest
{
    /// <summary>Matches against PIN, lot number, title number, survey number, or tax map number.</summary>
    public string? SearchTerm { get; set; }
    public Guid? BarangayId { get; set; }
    public Guid? MunicipalityId { get; set; }
}
