using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.PropertyIdentification;

namespace Prime.WebApi.Controllers;

/// <summary>
/// The Real Property Identification System's configuration (MRPAAO Ch. II §1;
/// docs/analysis/property-identification.md, step 10a-1): index numbers,
/// city districts and tax map sections. Role gating is Phase 12.
/// </summary>
[Route("api/property-identification")]
public class PropertyIdentificationController(IPropertyIdentificationService service) : ApiControllerBase
{
    [HttpGet("provinces")]
    public async Task<ActionResult<IReadOnlyList<ProvinceIndexDto>>> Provinces(CancellationToken ct) => HandleResult(await service.ListProvincesAsync(ct));

    [HttpPut("provinces/{id:guid}/index-number")]
    public async Task<ActionResult<ProvinceIndexDto>> SetProvinceIndex(Guid id, SetIndexNumberRequest request, CancellationToken ct) =>
        HandleResult(await service.SetProvinceIndexAsync(id, request, ct));

    [HttpGet("provinces/{provinceId:guid}/municipalities")]
    public async Task<ActionResult<IReadOnlyList<MunicipalityIndexDto>>> Municipalities(Guid provinceId, CancellationToken ct) =>
        HandleResult(await service.ListMunicipalitiesAsync(provinceId, ct));

    [HttpPut("municipalities/{id:guid}/index-number")]
    public async Task<ActionResult<MunicipalityIndexDto>> SetMunicipalityIndex(Guid id, SetIndexNumberRequest request, CancellationToken ct) =>
        HandleResult(await service.SetMunicipalityIndexAsync(id, request, ct));

    [HttpGet("municipalities/{municipalityId:guid}/districts")]
    public async Task<ActionResult<IReadOnlyList<CityDistrictDto>>> Districts(Guid municipalityId, CancellationToken ct) =>
        HandleResult(await service.ListDistrictsAsync(municipalityId, ct));

    [HttpPost("municipalities/{municipalityId:guid}/districts")]
    public async Task<ActionResult<CityDistrictDto>> CreateDistrict(Guid municipalityId, CreateCityDistrictRequest request, CancellationToken ct) =>
        HandleResult(await service.CreateDistrictAsync(municipalityId, request, ct));

    [HttpGet("municipalities/{municipalityId:guid}/barangays")]
    public async Task<ActionResult<IReadOnlyList<BarangayIndexDto>>> Barangays(Guid municipalityId, CancellationToken ct) =>
        HandleResult(await service.ListBarangaysAsync(municipalityId, ct));

    [HttpPut("barangays/{id:guid}/index-number")]
    public async Task<ActionResult<BarangayIndexDto>> SetBarangayIndex(Guid id, SetBarangayIndexRequest request, CancellationToken ct) =>
        HandleResult(await service.SetBarangayIndexAsync(id, request, ct));

    [HttpPost("barangays/{id:guid}/split")]
    public async Task<ActionResult<IReadOnlyList<BarangayIndexDto>>> SplitBarangay(Guid id, SplitBarangayRequest request, CancellationToken ct) =>
        HandleResult(await service.SplitBarangayAsync(id, request, ct));

    [HttpGet("barangays/{barangayId:guid}/sections")]
    public async Task<ActionResult<IReadOnlyList<TaxMapSectionDto>>> Sections(Guid barangayId, CancellationToken ct) =>
        HandleResult(await service.ListSectionsAsync(barangayId, ct));

    [HttpPost("barangays/{barangayId:guid}/sections")]
    public async Task<ActionResult<TaxMapSectionDto>> CreateSection(Guid barangayId, CreateTaxMapSectionRequest request, CancellationToken ct) =>
        HandleResult(await service.CreateSectionAsync(barangayId, request, ct));

    [HttpPost("sections/{id:guid}/retire")]
    public async Task<ActionResult<TaxMapSectionDto>> RetireSection(Guid id, RetireRequest request, CancellationToken ct) =>
        HandleResult(await service.RetireSectionAsync(id, request, ct));
}

/// <summary>A property's PIN and its history; placing its parcel in a tax map section gives the permanent PIN (step 10a-2).</summary>
[Route("api/properties/{propertyId:guid}/pin")]
public class PropertyPinController(IPinService pins) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PropertyPinDto>> Get(Guid propertyId, CancellationToken ct) => HandleResult(await pins.GetAsync(propertyId, ct));

    [HttpPost("place-in-section")]
    public async Task<ActionResult<PropertyPinDto>> PlaceInSection(Guid propertyId, PlaceInSectionRequest request, CancellationToken ct) =>
        HandleResult(await pins.PlaceInSectionAsync(propertyId, request, ct));

    /// <summary>Records (or withdraws) the office tie-up or the field confirmation of the temporary PIN (MRPAAO Ch. II §2 A).</summary>
    [HttpPost("tie-up")]
    public async Task<ActionResult<PropertyPinDto>> TieUp(Guid propertyId, RecordTieUpRequest request, CancellationToken ct) =>
        HandleResult(await pins.RecordTieUpAsync(propertyId, request, ct));
}
