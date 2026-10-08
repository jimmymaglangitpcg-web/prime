using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.ReferenceData;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

[Route("api/reference")]
public class ReferenceDataController(IReferenceDataService referenceDataService) : ApiControllerBase
{
    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("provinces")]
    public async Task<ActionResult<IReadOnlyList<ProvinceDto>>> GetProvinces(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetProvincesAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("municipalities")]
    public async Task<ActionResult<IReadOnlyList<MunicipalityDto>>> GetMunicipalities([FromQuery] Guid? provinceId, CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetMunicipalitiesAsync(provinceId, cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("barangays")]
    public async Task<ActionResult<IReadOnlyList<BarangayDto>>> GetBarangays([FromQuery] Guid? municipalityId, CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetBarangaysAsync(municipalityId, cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("zones")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetZones(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetZonesAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("classifications")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetClassifications(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetClassificationsAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("actual-uses")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetActualUses(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetActualUsesAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("sub-classifications")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetSubClassifications(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetSubClassificationsAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("ownership-types")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetOwnershipTypes(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetOwnershipTypesAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("property-types")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetPropertyTypes(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetPropertyTypesAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("road-types")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetRoadTypes(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetRoadTypesAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("conditions")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetConditions(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetConditionsAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("building-types")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetBuildingTypes(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetBuildingTypesAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("structural-types")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetStructuralTypes(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetStructuralTypesAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("machinery-types")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetMachineryTypes(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetMachineryTypesAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("tax-types")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetTaxTypes(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetTaxTypesAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("improvement-kinds")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetImprovementKinds(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetImprovementKindsAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("building-component-types")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetBuildingComponentTypes(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetBuildingComponentTypesAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("title-types")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetTitleTypes(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetTitleTypesAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("structural-parts")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetStructuralParts(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetStructuralPartsAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("structural-materials")]
    public async Task<ActionResult<IReadOnlyList<StructuralMaterialDto>>> GetStructuralMaterials(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetStructuralMaterialsAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("annotation-types")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetAnnotationTypes(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetAnnotationTypesAsync(cancellationToken));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("conveyance-modes")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetConveyanceModes(CancellationToken cancellationToken) =>
        Ok(await referenceDataService.GetConveyanceModesAsync(cancellationToken));
}
