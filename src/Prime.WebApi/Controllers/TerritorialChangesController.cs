using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.PropertyIdentification;

namespace Prime.WebApi.Controllers;

/// <summary>Territorial changes and barangay parts (docs/analysis/identification-numbering.md §4.3).</summary>
[Route("api")]
public class TerritorialChangesController(ITerritorialChangeService changes, IBarangayPartService parts) : ApiControllerBase
{
    [HttpPost("territorial-changes")]
    public async Task<ActionResult<TerritorialChangeDto>> Create(CreateTerritorialChangeRequest request, CancellationToken ct) =>
        HandleResult(await changes.CreateAsync(request, ct));

    [HttpPost("territorial-changes/{id:guid}/approve")]
    public async Task<ActionResult<TerritorialChangeDto>> Approve(Guid id, CancellationToken ct) => HandleResult(await changes.ApproveAsync(id, ct));

    [HttpPost("territorial-changes/{id:guid}/resume")]
    public async Task<ActionResult<TerritorialChangeDto>> Resume(Guid id, CancellationToken ct) => HandleResult(await changes.ResumeAsync(id, ct));

    [HttpGet("territorial-changes/{id:guid}")]
    public async Task<ActionResult<TerritorialChangeDto>> Get(Guid id, CancellationToken ct) => HandleResult(await changes.GetAsync(id, ct));

    [HttpGet("territorial-changes")]
    public async Task<ActionResult<IReadOnlyList<TerritorialChangeDto>>> List(CancellationToken ct) => HandleResult(await changes.ListAsync(ct));

    [HttpGet("properties/{propertyId:guid}/barangay-parts")]
    public async Task<ActionResult<IReadOnlyList<BarangayPartDto>>> ListParts(Guid propertyId, CancellationToken ct) =>
        HandleResult(await parts.ListAsync(propertyId, ct));

    [HttpPut("properties/{propertyId:guid}/barangay-parts")]
    public async Task<ActionResult<IReadOnlyList<BarangayPartDto>>> SetParts(Guid propertyId, SetBarangayPartsRequest request, CancellationToken ct) =>
        HandleResult(await parts.SetAsync(propertyId, request, ct));
}
