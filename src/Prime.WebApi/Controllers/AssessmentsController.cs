using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Appraisal;
using Prime.Application.Features.Assessments;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

[Route("api/assessments")]
public class AssessmentsController(IAssessmentService assessmentService, IAppraisalRecordService appraisals) : ApiControllerBase
{
    /// <summary>The assessment's appraisal record — the data a FAAS shows (docs/FORMS-REVISION-PLAN.md A7).</summary>
    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("{id:guid}/appraisal-record")]
    public async Task<ActionResult<AppraisalRecordDto>> GetAppraisalRecord(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await appraisals.GetAsync(id, cancellationToken));

    [RequirePermission(Permissions.AssessmentPrepare)]
    [HttpPost("preview")]
    public async Task<ActionResult<AssessmentPreviewDto>> Preview(CreateAssessmentRequest request, CancellationToken cancellationToken) =>
        HandleResult(await assessmentService.PreviewAsync(request, cancellationToken));

    /// <summary>The effectivity an assessment would take if made today (docs/analysis/valuation-foundation.md §4.2).</summary>
    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("effectivity")]
    public async Task<ActionResult<EffectivityDto>> Effectivity([FromQuery] Guid? transactionTypeId, [FromQuery] DateOnly? causeDate,
        [FromQuery] DateOnly? effectiveDate, [FromQuery] string? overrideReason, CancellationToken cancellationToken) =>
        HandleResult(await assessmentService.EffectivityAsync(transactionTypeId, causeDate, effectiveDate, overrideReason, cancellationToken));

    [RequirePermission(Permissions.AssessmentPrepare)]
    [HttpPost]
    public async Task<ActionResult<AssessmentDto>> Create(CreateAssessmentRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await assessmentService.CreateAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssessmentDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await assessmentService.GetByIdAsync(id, cancellationToken));

    [RequirePermission(Permissions.AssessmentPrepare)]
    [HttpPost("{id:guid}/submit-for-review")]
    public async Task<ActionResult<AssessmentDto>> SubmitForReview(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await assessmentService.SubmitForReviewAsync(id, cancellationToken));

    [RequirePermission(Permissions.AssessmentApprove)]
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<AssessmentDto>> Approve(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await assessmentService.ApproveAsync(id, cancellationToken));

    [RequirePermission(Permissions.AssessmentApprove)]
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<AssessmentDto>> Reject(Guid id, [FromBody] RejectAssessmentRequest request, CancellationToken cancellationToken) =>
        HandleResult(await assessmentService.RejectAsync(id, request.Reason, cancellationToken));

    [RequirePermission(Permissions.AssessmentPost)]
    [HttpPost("{id:guid}/post")]
    public async Task<ActionResult<AssessmentDto>> Post(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await assessmentService.PostAsync(id, cancellationToken));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("~/api/rpus/{rpuId:guid}/assessments")]
    public async Task<ActionResult<IReadOnlyList<AssessmentDto>>> ListByRpu(Guid rpuId, [FromQuery] DateOnly? asOfDate, CancellationToken cancellationToken) =>
        HandleResult(await assessmentService.ListByRpuAsync(rpuId, asOfDate, cancellationToken));
}

public sealed record RejectAssessmentRequest(string Reason);
