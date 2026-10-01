using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Submissions;

namespace Prime.WebApi.Controllers;

/// <summary>
/// What municipal offices send the province (docs/analysis/province-wide-operation.md §3.7, LP-6):
/// approved FAAS and TDs with their frozen printed copies, and the monthly assessment roll.
/// </summary>
[Route("api/submissions")]
public class SubmissionsController(ISubmissionService submissions) : ApiControllerBase
{
    /// <summary>TDs approved in the period (on the LGU's calendar), newest first, at most 500.</summary>
    [HttpGet("approved-documents")]
    public async Task<ActionResult<IReadOnlyList<ApprovedDocumentDto>>> Approved(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? municipalityId, CancellationToken ct) =>
        HandleResult(await submissions.ListApprovedAsync(municipalityId, from, to, ct));

    [HttpGet("rolls")]
    public async Task<ActionResult<IReadOnlyList<RollSubmissionDto>>> Rolls([FromQuery] Guid? municipalityId, CancellationToken ct) =>
        HandleResult(await submissions.ListRollsAsync(municipalityId, ct));

    [HttpGet("rolls/{id:guid}")]
    public async Task<ActionResult<RollSubmissionDto>> Roll(Guid id, CancellationToken ct) => HandleResult(await submissions.GetRollAsync(id, ct));

    /// <summary>Prepares, issues and submits the month's assessment rolls of a municipality.</summary>
    [HttpPost("rolls")]
    public async Task<ActionResult<RollSubmissionDto>> Submit(CreateRollSubmissionRequest request, CancellationToken ct) =>
        HandleResult(await submissions.SubmitRollAsync(request, ct));

    [HttpPost("rolls/{id:guid}/acknowledge")]
    public async Task<ActionResult<RollSubmissionDto>> Acknowledge(Guid id, ReviewRollSubmissionRequest request, CancellationToken ct) =>
        HandleResult(await submissions.AcknowledgeRollAsync(id, request, ct));

    [HttpPost("rolls/{id:guid}/return")]
    public async Task<ActionResult<RollSubmissionDto>> Return(Guid id, ReviewRollSubmissionRequest request, CancellationToken ct) =>
        HandleResult(await submissions.ReturnRollAsync(id, request, ct));
}
