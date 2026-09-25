using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Notices;

namespace Prime.WebApi.Controllers;

/// <summary>Notices of Assessment (LGC §§223, 226; docs/FORMS-REVISION-PLAN.md A6). Role gating is Phase 12.</summary>
[Route("api/notices")]
public class NoticesController(INoticeService notices) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<NoticeDto>> Generate(GenerateNoticeRequest request, CancellationToken ct) =>
        HandleCreated(await notices.GenerateAsync(request, ct), nameof(Get), dto => new { id = dto.Id });

    /// <summary>One notice for several of one declared owner's assessments (MRPAAO Att. 10).</summary>
    [HttpPost("combined")]
    public async Task<ActionResult<NoticeDto>> GenerateCombined(GenerateCombinedNoticeRequest request, CancellationToken ct) =>
        HandleResult(await notices.GenerateCombinedAsync(request, ct));

    [HttpGet("candidates")]
    public async Task<ActionResult<IReadOnlyList<NoticeCandidateDto>>> Candidates([FromQuery] Guid taxpayerId, CancellationToken ct) =>
        HandleResult(await notices.CandidatesAsync(taxpayerId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NoticeDto>> Get(Guid id, CancellationToken ct) => HandleResult(await notices.GetAsync(id, ct));

    [HttpGet("~/api/properties/{propertyId:guid}/notices")]
    public async Task<ActionResult<IReadOnlyList<NoticeDto>>> ListByProperty(Guid propertyId, CancellationToken ct) =>
        HandleResult(await notices.ListByPropertyAsync(propertyId, ct));

    [HttpPost("{id:guid}/issue")]
    public async Task<ActionResult<NoticeDto>> Issue(Guid id, CancellationToken ct) => HandleResult(await notices.IssueAsync(id, ct));

    [HttpPost("{id:guid}/service")]
    public async Task<ActionResult<NoticeDto>> RecordService(Guid id, RecordNoticeServiceRequest request, CancellationToken ct) =>
        HandleResult(await notices.RecordServiceAsync(id, request, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<NoticeDto>> Cancel(Guid id, NoticeReasonRequest request, CancellationToken ct) =>
        HandleResult(await notices.CancelAsync(id, request.Reason, ct));
}
