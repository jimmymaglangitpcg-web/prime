using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.AssessmentLevels;

namespace Prime.WebApi.Controllers;

[Route("api/assessment-levels")]
public class AssessmentLevelsController(IAssessmentLevelService assessmentLevelService) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AssessmentLevelDto>> Create(CreateAssessmentLevelRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await assessmentLevelService.CreateAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssessmentLevelDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await assessmentLevelService.GetByIdAsync(id, cancellationToken));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AssessmentLevelDto>>> List(CancellationToken cancellationToken) =>
        HandleResult(await assessmentLevelService.ListAsync(cancellationToken));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<AssessmentLevelDto>> Approve(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await assessmentLevelService.ApproveAsync(id, cancellationToken));
}
