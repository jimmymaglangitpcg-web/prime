using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Exemptions;

namespace Prime.WebApi.Controllers;

/// <summary>Exemption types and claims (CLAUDE.md §43; docs/analysis/assessment-listing-exemptions.md §4.1). Role gating is Phase 12.</summary>
[Route("api/exemptions")]
public class ExemptionsController(IExemptionService exemptions) : ApiControllerBase
{
    [HttpGet("types")]
    public async Task<ActionResult<IReadOnlyList<ExemptionTypeDto>>> ListTypes([FromQuery] bool inForceOnly, CancellationToken ct) =>
        HandleResult(await exemptions.ListTypesAsync(inForceOnly, ct));

    [HttpPost("types")]
    public async Task<ActionResult<ExemptionTypeDto>> CreateType(CreateExemptionTypeRequest request, CancellationToken ct) =>
        HandleResult(await exemptions.CreateTypeAsync(request, ct));

    [HttpPost("types/{id:guid}/approve")]
    public async Task<ActionResult<ExemptionTypeDto>> ApproveType(Guid id, CancellationToken ct) => HandleResult(await exemptions.ApproveTypeAsync(id, ct));

    /// <summary>Open claims, overdue proof first.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PropertyExemptionDto>>> ListOpen(CancellationToken ct) => HandleResult(await exemptions.ListOpenAsync(ct));

    [HttpPost]
    public async Task<ActionResult<PropertyExemptionDto>> Claim(ClaimExemptionRequest request, CancellationToken ct) =>
        HandleCreated(await exemptions.ClaimAsync(request, ct), nameof(Get), dto => new { id = dto.Id });

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PropertyExemptionDto>> Get(Guid id, CancellationToken ct) => HandleResult(await exemptions.GetAsync(id, ct));

    [HttpGet("~/api/properties/{propertyId:guid}/exemptions")]
    public async Task<ActionResult<IReadOnlyList<PropertyExemptionDto>>> ListByProperty(Guid propertyId, CancellationToken ct) =>
        HandleResult(await exemptions.ListByPropertyAsync(propertyId, ct));

    [HttpPost("{id:guid}/evidence")]
    public async Task<ActionResult<PropertyExemptionDto>> AddEvidence(Guid id, AddExemptionEvidenceRequest request, CancellationToken ct) =>
        HandleResult(await exemptions.AddEvidenceAsync(id, request, ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<PropertyExemptionDto>> Approve(Guid id, ApproveExemptionRequest request, CancellationToken ct) =>
        HandleResult(await exemptions.ApproveAsync(id, request, ct));

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<PropertyExemptionDto>> Reject(Guid id, ExemptionReasonRequest request, CancellationToken ct) =>
        HandleResult(await exemptions.RejectAsync(id, request.Reason, ct));

    [HttpPost("{id:guid}/end")]
    public async Task<ActionResult<PropertyExemptionDto>> End(Guid id, EndExemptionRequest request, CancellationToken ct) =>
        HandleResult(await exemptions.EndAsync(id, request, ct));
}
