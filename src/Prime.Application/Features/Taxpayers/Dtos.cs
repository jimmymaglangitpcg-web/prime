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
    string? Email,
    Sex? Sex = null);

/// <summary>
/// Corrects a taxpayer's contact and personal details (the LAM forms print them; records-and-forms.md §4.1). The
/// name and type are not changed here. The reason goes to the audit log with the old and new values.
/// </summary>
public sealed record UpdateTaxpayerDetailsRequest(string? Tin, string? Address, string? ContactNumber, string? Email, Sex? Sex, string Reason);

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
    DateTimeOffset CreatedAt,
    bool Limited = false,
    Sex? Sex = null,
    /// <summary>TIN, contact, e-mail and address are masked: an individual's, for a user without taxpayer.view-personal (Q16).</summary>
    bool PersonalDataMasked = false,
    /// <summary>Row version, echoed in If-Match when editing details (production-hardening.md §4.4).</summary>
    uint RowVersion = 0);

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
    PropertyPartyRole Role = PropertyPartyRole.Owner,
    Guid? RpuId = null);

public sealed record EndPropertyPartyRequest(DateOnly EndDate, string Reason);
