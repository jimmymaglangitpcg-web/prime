using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Notices;
using Prime.Application.Features.Transactions;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>Notices of Cancellation (docs/analysis/assessment-listing-exemptions.md §4.4, Q11).</summary>
[Route("api/notices-of-cancellation")]
public class NoticesOfCancellationController(INoticeOfCancellationService notices) : ApiControllerBase
{
    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NoticeOfCancellationDto>> Get(Guid id, CancellationToken ct) => HandleResult(await notices.GetAsync(id, ct));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("~/api/properties/{propertyId:guid}/notices-of-cancellation")]
    public async Task<ActionResult<IReadOnlyList<NoticeOfCancellationDto>>> ListByProperty(Guid propertyId, CancellationToken ct) =>
        HandleResult(await notices.ListByPropertyAsync(propertyId, ct));

    [RequirePermission(Permissions.NoticeIssue)]
    [HttpPost("{id:guid}/issue")]
    public async Task<ActionResult<NoticeOfCancellationDto>> Issue(Guid id, CancellationToken ct) => HandleResult(await notices.IssueAsync(id, ct));

    [RequirePermission(Permissions.NoticeIssue)]
    [HttpPost("{id:guid}/service")]
    public async Task<ActionResult<NoticeOfCancellationDto>> RecordService(Guid id, RecordNoticeServiceRequest request, CancellationToken ct) =>
        HandleResult(await notices.RecordServiceAsync(id, request, ct));

    [RequirePermission(Permissions.NoticeIssue)]
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<NoticeOfCancellationDto>> Cancel(Guid id, NoticeReasonRequest request, CancellationToken ct) =>
        HandleResult(await notices.CancelAsync(id, request.Reason, ct));
}

/// <summary>Discovery summonses on a new-discovery transaction (docs/analysis/assessment-listing-exemptions.md §4.4, Q10).</summary>
[Route("api/transactions/{transactionId:guid}/discovery")]
public class DiscoveryController(IDiscoverySummonsService summonses) : ApiControllerBase
{
    [RequirePermission(Permissions.PropertyView)]
    [HttpGet]
    public async Task<ActionResult<DiscoveryDto>> Get(Guid transactionId, CancellationToken ct) => HandleResult(await summonses.GetAsync(transactionId, ct));

    [RequirePermission(Permissions.TransactionPrepare)]
    [HttpPost("summonses")]
    public async Task<ActionResult<DiscoveryDto>> Issue(Guid transactionId, IssueSummonsRequest request, CancellationToken ct) =>
        HandleResult(await summonses.IssueAsync(transactionId, request, ct));

    [RequirePermission(Permissions.TransactionPrepare)]
    [HttpPost("~/api/summonses/{id:guid}/service")]
    public async Task<ActionResult<DiscoveryDto>> RecordService(Guid id, RecordSummonsServiceRequest request, CancellationToken ct) =>
        HandleResult(await summonses.RecordServiceAsync(id, request, ct));

    [RequirePermission(Permissions.TransactionPrepare)]
    [HttpPost("~/api/summonses/{id:guid}/outcome")]
    public async Task<ActionResult<DiscoveryDto>> RecordOutcome(Guid id, SummonsOutcomeRequest request, CancellationToken ct) =>
        HandleResult(await summonses.RecordOutcomeAsync(id, request, ct));

    [RequirePermission(Permissions.TransactionPrepare)]
    [HttpPost("verification")]
    public async Task<ActionResult<DiscoveryDto>> RecordVerification(Guid transactionId, VerificationRequest request, CancellationToken ct) =>
        HandleResult(await summonses.RecordVerificationAsync(transactionId, request, ct));
}
