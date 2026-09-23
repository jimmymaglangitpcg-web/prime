using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.ReferenceData;

namespace Prime.WebApi.Controllers;

[Route("api/reference")]
public class ReferenceDataController(IReferenceDataService referenceDataService) : ApiControllerBase
{
    [HttpGet("provinces")]
    public async Task<ActionResult<IReadOnlyList<ProvinceDto>>> GetProvinces(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetProvincesAsync(cancellationToken));

    [HttpGet("municipalities")]
    public async Task<ActionResult<IReadOnlyList<MunicipalityDto>>> GetMunicipalities([FromQuery] Guid? provinceId, CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetMunicipalitiesAsync(provinceId, cancellationToken));

    [HttpGet("barangays")]
    public async Task<ActionResult<IReadOnlyList<BarangayDto>>> GetBarangays([FromQuery] Guid? municipalityId, CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetBarangaysAsync(municipalityId, cancellationToken));

    [HttpGet("zones")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetZones(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetZonesAsync(cancellationToken));

    [HttpGet("classifications")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetClassifications(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetClassificationsAsync(cancellationToken));

    [HttpGet("actual-uses")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetActualUses(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetActualUsesAsync(cancellationToken));

    [HttpGet("sub-classifications")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetSubClassifications(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetSubClassificationsAsync(cancellationToken));

    [HttpGet("ownership-types")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetOwnershipTypes(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetOwnershipTypesAsync(cancellationToken));

    [HttpGet("property-types")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetPropertyTypes(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetPropertyTypesAsync(cancellationToken));
}
