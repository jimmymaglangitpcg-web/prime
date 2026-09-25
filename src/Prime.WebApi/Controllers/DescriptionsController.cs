using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common;
using Prime.Application.Features.Buildings;
using Prime.Application.Features.Descriptions;
using Prime.Application.Features.MachineryUnits;
using Prime.Application.Features.Properties;
using Prime.Application.Features.Transactions;

namespace Prime.WebApi.Controllers;

/// <summary>
/// Descriptive fields printed on the FAAS and TD (docs/analysis/mrpaao-forms-model.md
/// §10). Each call returns the record as it now stands.
/// </summary>
[Route("api")]
public class DescriptionsController(
    IDescriptionService descriptions, IPropertyService properties, IBuildingService buildings,
    IMachineryService machinery, ITransactionService transactions) : ApiControllerBase
{
    [HttpPut("properties/{id:guid}/description")]
    public async Task<ActionResult<PropertyProfileDto>> UpdateProperty(Guid id, UpdatePropertyDescriptionRequest request, CancellationToken ct) =>
        await Then(await descriptions.UpdatePropertyAsync(id, request, ct), () => properties.GetProfileAsync(id, ct));

    [HttpPut("buildings/{id:guid}/description")]
    public async Task<ActionResult<BuildingDto>> UpdateBuilding(Guid id, UpdateBuildingDescriptionRequest request, CancellationToken ct) =>
        await Then(await descriptions.UpdateBuildingAsync(id, request, ct), () => buildings.GetByIdAsync(id, ct));

    [HttpPost("buildings/{id:guid}/floors")]
    public async Task<ActionResult<BuildingDto>> AddFloor(Guid id, AddBuildingFloorRequest request, CancellationToken ct) =>
        await Then(await descriptions.AddBuildingFloorAsync(id, request, ct), () => buildings.GetByIdAsync(id, ct));

    [HttpPost("buildings/{id:guid}/materials")]
    public async Task<ActionResult<BuildingDto>> AddMaterial(Guid id, AddBuildingMaterialRequest request, CancellationToken ct) =>
        await Then(await descriptions.AddBuildingMaterialAsync(id, request, ct), () => buildings.GetByIdAsync(id, ct));

    [HttpPut("machinery/{id:guid}/description")]
    public async Task<ActionResult<MachineryDto>> UpdateMachinery(Guid id, UpdateMachineryDescriptionRequest request, CancellationToken ct) =>
        await Then(await descriptions.UpdateMachineryAsync(id, request, ct), () => machinery.GetByIdAsync(id, ct));

    [HttpPut("transactions/{id:guid}/tax-clearance")]
    public async Task<ActionResult<PropertyTransactionDto>> SetTaxClearance(Guid id, SetTransferTaxClearanceRequest request, CancellationToken ct) =>
        await Then(await descriptions.SetTransferTaxClearanceAsync(id, request, ct), () => transactions.GetAsync(id, ct));

    private async Task<ActionResult<T>> Then<T>(Result done, Func<Task<Result<T>>> reload) =>
        HandleResult(done.IsSuccess ? await reload() : Result.Failure<T>(done.Code!, done.Message!));
}
