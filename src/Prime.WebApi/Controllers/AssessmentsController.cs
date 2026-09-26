using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Appraisal;
using Prime.Application.Features.Assessments;

namespace Prime.WebApi.Controllers;

[Route("api/assessments")]
public class AssessmentsController(IAssessmentService assessmentService, IAppraisalRecordService appraisals) : ApiControllerBase
{
    /// <summary>The assessment's appraisal record — the data a FAAS shows (docs/FORMS-REVISION-PLAN.md A7).</summary>
    [HttpGet("{id:guid}/appraisal-record")]
    public async Task<ActionResult<AppraisalRecordDto>> GetAppraisalRecord(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await appraisals.GetAsync(id, cancellationToken));

    [HttpPost("preview")]
    public async Task<ActionResult<AssessmentPreviewDto>> Preview(CreateAssessmentRequest request, CancellationToken cancellationToken) =>
        HandleResult(await assessmentService.PreviewAsync(request, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<AssessmentDto>> Create(CreateAssessmentRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await assessmentService.CreateAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssessmentDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await assessmentService.GetByIdAsync(id, cancellationToken));

    [HttpPost("{id:guid}/submit-for-review")]
    public async Task<ActionResult<AssessmentDto>> SubmitForReview(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await assessmentService.SubmitForReviewAsync(id, cancellationToken));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<AssessmentDto>> Approve(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await assessmentService.ApproveAsync(id, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<AssessmentDto>> Reject(Guid id, [FromBody] RejectAssessmentRequest request, CancellationToken cancellationToken) =>
        HandleResult(await assessmentService.RejectAsync(id, request.Reason, cancellationToken));

    [HttpPost("{id:guid}/post")]
    public async Task<ActionResult<AssessmentDto>> Post(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await assessmentService.PostAsync(id, cancellationToken));

    [HttpGet("~/api/rpus/{rpuId:guid}/assessments")]
    public async Task<ActionResult<IReadOnlyList<AssessmentDto>>> ListByRpu(Guid rpuId, [FromQuery] DateOnly? asOfDate, CancellationToken cancellationToken) =>
        HandleResult(await assessmentService.ListByRpuAsync(rpuId, asOfDate, cancellationToken));
}

public sealed record RejectAssessmentRequest(string Reason);
