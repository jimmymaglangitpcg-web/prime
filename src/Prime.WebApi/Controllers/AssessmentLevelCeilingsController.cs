using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.AssessmentLevels;

namespace Prime.WebApi.Controllers;

/// <summary>Statutory maximum assessment levels (docs/analysis/assessment-listing-exemptions.md §4.2).</summary>
[Route("api/assessment-level-ceilings")]
public class AssessmentLevelCeilingsController(IAssessmentLevelCeilingService ceilings) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AssessmentLevelCeilingDto>>> List([FromQuery] bool inForceOnly, CancellationToken ct) =>
        HandleResult(await ceilings.ListAsync(inForceOnly, ct));

    [HttpPost]
    public async Task<ActionResult<AssessmentLevelCeilingDto>> Create(CreateAssessmentLevelCeilingRequest request, CancellationToken ct) =>
        HandleResult(await ceilings.CreateAsync(request, ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<AssessmentLevelCeilingDto>> Approve(Guid id, CancellationToken ct) =>
        HandleResult(await ceilings.ApproveAsync(id, ct));
}
