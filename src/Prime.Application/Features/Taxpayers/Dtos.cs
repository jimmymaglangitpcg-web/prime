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

public sealed record AddPropertyOwnerRequest(
    Guid PropertyId,
    Guid TaxpayerId,
    Guid OwnershipTypeId,
    decimal OwnershipPercentage,
    DateOnly StartDate);
