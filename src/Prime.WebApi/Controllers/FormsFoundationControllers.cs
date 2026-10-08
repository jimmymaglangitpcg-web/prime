using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Prime.Application.Features.Approvals;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Numbering;
using Prime.Domain.Enums;
using Prime.Application.Common.Security;
using Prime.WebApi.Auditing;
using Prime.WebApi.Authorization;
using Prime.WebApi.Security;

namespace Prime.WebApi.Controllers;

/// <summary>Numbering schemes (docs/FORMS-REVISION-PLAN.md §4.4). Role gating is Phase 12.</summary>
[Route("api/numbering-schemes")]
public class NumberingSchemesController(INumberingService numbering) : ApiControllerBase
{
    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NumberingSchemeDto>>> List(CancellationToken ct) => HandleResult(await numbering.ListAsync(ct));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NumberingSchemeDto>> Get(Guid id, CancellationToken ct) => HandleResult(await numbering.GetAsync(id, ct));

    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost]
    public async Task<ActionResult<NumberingSchemeDto>> Create(CreateNumberingSchemeRequest request, CancellationToken ct) =>
        HandleCreated(await numbering.CreateAsync(request, ct), nameof(Get), dto => new { id = dto.Id });

    [RequirePermission(Permissions.ConfigApprove)]
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<NumberingSchemeDto>> Approve(Guid id, CancellationToken ct) => HandleResult(await numbering.ApproveAsync(id, ct));
}

/// <summary>Form definitions, preview and issuance (docs/FORMS-REVISION-PLAN.md §4.1–§4.3).</summary>
[Route("api/forms")]
public class FormsController(IFormService forms) : ApiControllerBase
{
    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("definitions")]
    public async Task<ActionResult<IReadOnlyList<FormDefinitionDto>>> ListDefinitions(CancellationToken ct) =>
        HandleResult(await forms.ListDefinitionsAsync(ct));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("definitions/{id:guid}")]
    public async Task<ActionResult<FormDefinitionDto>> GetDefinition(Guid id, CancellationToken ct) =>
        HandleResult(await forms.GetDefinitionAsync(id, ct));

    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost("definitions")]
    public async Task<ActionResult<FormDefinitionDto>> CreateDefinition(CreateFormDefinitionRequest request, CancellationToken ct) =>
        HandleCreated(await forms.CreateDefinitionAsync(request, ct), nameof(GetDefinition), dto => new { id = dto.Id });

    [RequirePermission(Permissions.ConfigApprove)]
    [HttpPost("definitions/{id:guid}/approve")]
    public async Task<ActionResult<FormDefinitionDto>> ApproveDefinition(Guid id, CancellationToken ct) =>
        HandleResult(await forms.ApproveDefinitionAsync(id, ct));

    /// <summary>Renders without saving — e.g. a draft bill. HTML is returned inside JSON, never served as a page.</summary>
    [RequirePermission(Permissions.FormsPreview)]
    [HttpPost("preview")]
    public async Task<ActionResult<FormPreviewDto>> Preview(IssueFormRequest request, CancellationToken ct) =>
        HandleResult(await forms.PreviewAsync(request.FormCode, request.SubjectId, ct));

    [RequirePermission(Permissions.FormsIssue)]
    [HttpPost("issue")]
    [AuditExport("IssuedForms", "Form issued")]
    [EnableRateLimiting(RateLimiting.StrictPolicy)]
    public async Task<ActionResult<IssuedFormDto>> Issue(IssueFormRequest request, CancellationToken ct) =>
        HandleResult(await forms.IssueAsync(request, ct));

    [RequirePermission(Permissions.RecordsView)]
    [HttpGet("~/api/issued-forms/{id:guid}")]
    public async Task<ActionResult<IssuedFormDto>> GetIssued(Guid id, CancellationToken ct) => HandleResult(await forms.GetIssuedAsync(id, ct));

    [RequirePermission(Permissions.RecordsView)]
    [HttpGet("~/api/issued-forms")]
    public async Task<ActionResult<IReadOnlyList<IssuedFormDto>>> ListIssued([FromQuery] Guid subjectId, CancellationToken ct) =>
        HandleResult(await forms.ListIssuedAsync(subjectId, ct));

    [RequirePermission(Permissions.FormsIssue)]
    [HttpPost("~/api/issued-forms/{id:guid}/cancel")]
    public async Task<ActionResult<IssuedFormDto>> CancelIssued(Guid id, [FromBody] CancelIssuedFormRequest request, CancellationToken ct) =>
        HandleResult(await forms.CancelIssuedAsync(id, request.Reason, ct));
}

/// <summary>Approval chains and signed steps (docs/FORMS-REVISION-PLAN.md §4.5).</summary>
[Route("api/approval-chains")]
public class ApprovalChainsController(IApprovalChainService chains, Prime.Application.Common.Interfaces.IClock clock) : ApiControllerBase
{
    /// <summary>Records whose next approval step the signed-in user may sign today (docs/analysis/province-wide-operation.md §3.4).</summary>
    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("~/api/approvals/awaiting")]
    public async Task<ActionResult<IReadOnlyList<ApprovalQueueItemDto>>> Awaiting([FromQuery] Guid? municipalityId, CancellationToken ct) =>
        HandleResult(await chains.ListAwaitingAsync(clock.Today, ct, municipalityId));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApprovalChainDto>>> List(CancellationToken ct) => HandleResult(await chains.ListAsync(ct));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApprovalChainDto>> Get(Guid id, CancellationToken ct) => HandleResult(await chains.GetAsync(id, ct));

    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost]
    public async Task<ActionResult<ApprovalChainDto>> Create(CreateApprovalChainRequest request, CancellationToken ct) =>
        HandleCreated(await chains.CreateAsync(request, ct), nameof(Get), dto => new { id = dto.Id });

    [RequirePermission(Permissions.ConfigApprove)]
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ApprovalChainDto>> Approve(Guid id, CancellationToken ct) => HandleResult(await chains.ApproveAsync(id, ct));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("~/api/approval-records")]
    public async Task<ActionResult<IReadOnlyList<ApprovalRecordDto>>> ListRecords([FromQuery] ApprovalSubjectType subjectType, [FromQuery] Guid subjectId,
        CancellationToken ct) => HandleResult(await chains.ListRecordsAsync(subjectType, subjectId, ct));
}
