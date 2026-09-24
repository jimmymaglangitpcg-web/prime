using Prime.Domain.Enums;

namespace Prime.Application.Features.Taxpayers;

public sealed record CreateTaxpayerRequest(
    TaxpayerType TaxpayerType,
    string? LastName,
    string? FirstName,
    string? MiddleName,
    string? Suffix,
    string? CorporateName,
    string? Tin,
    string? Address,
    Guid? BarangayId,
    Guid? MunicipalityId,
    Guid? ProvinceId,
    string? ContactNumber,
    string? Email);

public sealed record TaxpayerDto(
    Guid Id,
    TaxpayerType TaxpayerType,
    string DisplayName,
    string? LastName,
    string? FirstName,
    string? MiddleName,
    string? Suffix,
    string? CorporateName,
    string? Tin,
    string? Address,
    string? ContactNumber,
    string? Email,
    RecordStatus Status,
    DateTimeOffset CreatedAt);

public sealed class TaxpayerSearchRequest : Common.PagedRequest
{
    /// <summary>Matches against name (individual or corporate) or TIN.</summary>
    public string? SearchTerm { get; set; }
}

/// <summary>
/// Role defaults to Owner (existing callers). TaxpayerId is omitted only for
/// an unknown owner; OwnershipTypeId is required for owners only.
/// </summary>
public sealed record AddPropertyOwnerRequest(
    Guid PropertyId,
    Guid? TaxpayerId,
    Guid? OwnershipTypeId,
    decimal OwnershipPercentage,
    DateOnly StartDate,
    PropertyPartyRole Role = PropertyPartyRole.Owner);

public sealed record EndPropertyPartyRequest(DateOnly EndDate, string Reason);
