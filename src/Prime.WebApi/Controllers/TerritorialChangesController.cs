using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.PropertyIdentification;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>Territorial changes and barangay parts (docs/analysis/identification-numbering.md §4.3).</summary>
[Route("api")]
public class TerritorialChangesController(ITerritorialChangeService changes, IBarangayPartService parts) : ApiControllerBase
{
    [RequirePermission(Permissions.PinManage)]
    [HttpPost("territorial-changes")]
    public async Task<ActionResult<TerritorialChangeDto>> Create(CreateTerritorialChangeRequest request, CancellationToken ct) =>
        HandleResult(await changes.CreateAsync(request, ct));

    [RequirePermission(Permissions.PinApprove)]
    [HttpPost("territorial-changes/{id:guid}/approve")]
    public async Task<ActionResult<TerritorialChangeDto>> Approve(Guid id, CancellationToken ct) => HandleResult(await changes.ApproveAsync(id, ct));

    [RequirePermission(Permissions.PinManage)]
    [HttpPost("territorial-changes/{id:guid}/resume")]
    public async Task<ActionResult<TerritorialChangeDto>> Resume(Guid id, CancellationToken ct) => HandleResult(await changes.ResumeAsync(id, ct));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("territorial-changes/{id:guid}")]
    public async Task<ActionResult<TerritorialChangeDto>> Get(Guid id, CancellationToken ct) => HandleResult(await changes.GetAsync(id, ct));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("territorial-changes")]
    public async Task<ActionResult<IReadOnlyList<TerritorialChangeDto>>> List(CancellationToken ct) => HandleResult(await changes.ListAsync(ct));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("properties/{propertyId:guid}/barangay-parts")]
    public async Task<ActionResult<IReadOnlyList<BarangayPartDto>>> ListParts(Guid propertyId, CancellationToken ct) =>
        HandleResult(await parts.ListAsync(propertyId, ct));

    [RequirePermission(Permissions.PinManage)]
    [HttpPut("properties/{propertyId:guid}/barangay-parts")]
    public async Task<ActionResult<IReadOnlyList<BarangayPartDto>>> SetParts(Guid propertyId, SetBarangayPartsRequest request, CancellationToken ct) =>
        HandleResult(await parts.SetAsync(propertyId, request, ct));
}
