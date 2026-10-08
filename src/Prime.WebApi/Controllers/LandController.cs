using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Lands;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

[Route("api/land")]
public class LandController(ILandService landService) : ApiControllerBase
{
    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPost]
    public async Task<ActionResult<LandDto>> Create(CreateLandRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await landService.CreateAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LandDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await landService.GetByIdAsync(id, cancellationToken));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("~/api/rpus/{rpuId:guid}/land")]
    public async Task<ActionResult<LandDto>> GetByRpu(Guid rpuId, CancellationToken cancellationToken) =>
        HandleResult(await landService.GetByRpuAsync(rpuId, cancellationToken));

    /// <summary>Appraisal rows (docs/analysis/mrpaao-forms-model.md §8.3): strips, improvements, adjustments.</summary>
    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPost("{id:guid}/strips")]
    public async Task<ActionResult<LandDto>> AddStrip(Guid id, AddLandStripRequest request, CancellationToken cancellationToken) =>
        HandleResult(await landService.AddStripAsync(id, request, cancellationToken));

    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPost("{id:guid}/improvements")]
    public async Task<ActionResult<LandDto>> AddImprovement(Guid id, AddLandImprovementRequest request, CancellationToken cancellationToken) =>
        HandleResult(await landService.AddImprovementAsync(id, request, cancellationToken));

    /// <summary>Road, corner, distances and subdivision status, which the adjustment factors read; with a reason (audited).</summary>
    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPut("{id:guid}/appraisal-inputs")]
    public async Task<ActionResult<LandDto>> UpdateAppraisalInputs(Guid id, UpdateLandAppraisalInputsRequest request, CancellationToken cancellationToken) =>
        HandleResult(await landService.UpdateAppraisalInputsAsync(id, request, cancellationToken));

    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPost("{id:guid}/adjustments")]
    public async Task<ActionResult<LandDto>> AddAdjustment(Guid id, AddLandAdjustmentRequest request, CancellationToken cancellationToken) =>
        HandleResult(await landService.AddAdjustmentAsync(id, request, cancellationToken));
}
