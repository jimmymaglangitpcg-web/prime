using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Prime.Application.Common;
using Prime.Application.Features.GeneralRevision;
using Prime.Application.Common.Security;
using Prime.WebApi.Auditing;
using Prime.WebApi.Authorization;
using Prime.WebApi.Security;

namespace Prime.WebApi.Controllers;

/// <summary>General revision programmes (docs/analysis/smv-preparation-general-revision.md §4.6).</summary>
[Route("api/general-revision/programmes")]
public class GeneralRevisionProgrammesController(
    IGeneralRevisionProgrammeService programmes, IGeneralRevisionRecordsService records, IGeneralRevisionCompletionService completion) : ApiControllerBase
{
    [RequirePermission(Permissions.GeneralRevisionView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GeneralRevisionSummaryDto>>> List(CancellationToken ct) => HandleResult(await programmes.ListAsync(ct));

    [RequirePermission(Permissions.GeneralRevisionManage)]
    [HttpPost]
    public async Task<ActionResult<GeneralRevisionDto>> Create(CreateGeneralRevisionRequest request, CancellationToken ct) =>
        HandleResult(await programmes.CreateAsync(request, ct));

    [RequirePermission(Permissions.GeneralRevisionView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GeneralRevisionDto>> Get(Guid id, CancellationToken ct) => HandleResult(await programmes.GetAsync(id, ct));

    [RequirePermission(Permissions.GeneralRevisionManage)]
    [HttpPut("{id:guid}/references")]
    public async Task<ActionResult<GeneralRevisionDto>> UpdateReferences(Guid id, UpdateGeneralRevisionReferencesRequest request, CancellationToken ct) =>
        HandleResult(await programmes.UpdateReferencesAsync(id, request, ct));

    [RequirePermission(Permissions.GeneralRevisionView)]
    [HttpGet("{id:guid}/items")]
    public async Task<ActionResult<PagedResult<GeneralRevisionItemDto>>> Items(Guid id, [FromQuery] GeneralRevisionItemSearchRequest request, CancellationToken ct) =>
        HandleResult(await programmes.SearchItemsAsync(id, request, ct));

    [RequirePermission(Permissions.GeneralRevisionManage)]
    [HttpPost("{id:guid}/runs")]
    public async Task<ActionResult<GeneralRevisionRunDto>> StartRun(Guid id, StartGeneralRevisionRunRequest request, CancellationToken ct) =>
        HandleResult(await programmes.StartRunAsync(id, request, ct));

    [RequirePermission(Permissions.GeneralRevisionView)]
    [HttpGet("{id:guid}/runs/{runId:guid}/issues")]
    public async Task<ActionResult<IReadOnlyList<GeneralRevisionRunIssueDto>>> RunIssues(Guid id, Guid runId, CancellationToken ct) =>
        HandleResult(await programmes.ListRunIssuesAsync(id, runId, ct));

    [RequirePermission(Permissions.GeneralRevisionManage)]
    [HttpPost("{id:guid}/inspections/assign")]
    public async Task<ActionResult<int>> AssignInspection(Guid id, AssignGeneralRevisionInspectionRequest request, CancellationToken ct) =>
        HandleResult(await programmes.AssignInspectionAsync(id, request, ct));

    [RequirePermission(Permissions.GeneralRevisionManage)]
    [HttpPost("{id:guid}/items/{itemId:guid}/inspection")]
    public async Task<ActionResult<GeneralRevisionItemDto>> RecordInspection(Guid id, Guid itemId, RecordGeneralRevisionInspectionRequest request, CancellationToken ct) =>
        HandleResult(await programmes.RecordInspectionAsync(id, itemId, request, ct));

    [RequirePermission(Permissions.GeneralRevisionView)]
    [HttpGet("{id:guid}/notices")]
    public async Task<ActionResult<PagedResult<GeneralRevisionNoticeDto>>> Notices(Guid id, [FromQuery] GeneralRevisionNoticeSearchRequest request, CancellationToken ct) =>
        HandleResult(await records.SearchNoticesAsync(id, request, ct));

    [RequirePermission(Permissions.GeneralRevisionManage)]
    [HttpPost("{id:guid}/notices/service")]
    public async Task<ActionResult<NoticeServiceResultDto>> RecordNoticeService(Guid id, RecordGeneralRevisionNoticeServiceRequest request, CancellationToken ct) =>
        HandleResult(await records.RecordNoticeServiceAsync(id, request, ct));

    [RequirePermission(Permissions.GeneralRevisionView)]
    [HttpGet("{id:guid}/roll-gates")]
    public async Task<ActionResult<IReadOnlyList<RollGateDto>>> RollGates(Guid id, CancellationToken ct) => HandleResult(await records.RollGatesAsync(id, ct));

    [RequirePermission(Permissions.GeneralRevisionView)]
    [HttpGet("{id:guid}/register-runs")]
    public async Task<ActionResult<IReadOnlyList<GeneralRevisionRegisterRunDto>>> RegisterRuns(Guid id, CancellationToken ct) =>
        HandleResult(await records.ListRegisterRunsAsync(id, ct));

    [RequirePermission(Permissions.GeneralRevisionManage)]
    [HttpPost("{id:guid}/register-runs")]
    [AuditExport("RegisterRuns", "General revision register run")]
    [EnableRateLimiting(RateLimiting.StrictPolicy)]
    public async Task<ActionResult<IReadOnlyList<GeneralRevisionRegisterRunDto>>> CreateRegisterRuns(Guid id, CreateGeneralRevisionRegisterRunsRequest request, CancellationToken ct) =>
        HandleResult(await records.CreateRegisterRunsAsync(id, request, ct));

    [RequirePermission(Permissions.GeneralRevisionManage)]
    [HttpPost("{id:guid}/items/{itemId:guid}/exclude")]
    public async Task<ActionResult<GeneralRevisionItemDto>> Exclude(Guid id, Guid itemId, ExcludeGeneralRevisionItemRequest request, CancellationToken ct) =>
        HandleResult(await completion.ExcludeItemAsync(id, itemId, request, ct));

    [RequirePermission(Permissions.GeneralRevisionManage)]
    [HttpPost("{id:guid}/items/{itemId:guid}/include")]
    public async Task<ActionResult<GeneralRevisionItemDto>> Include(Guid id, Guid itemId, CancellationToken ct) =>
        HandleResult(await completion.IncludeItemAsync(id, itemId, ct));

    [RequirePermission(Permissions.GeneralRevisionView)]
    [HttpGet("{id:guid}/readiness")]
    public async Task<ActionResult<GeneralRevisionReadinessDto>> Readiness(Guid id, CancellationToken ct) => HandleResult(await completion.ReadinessAsync(id, ct));

    [RequirePermission(Permissions.GeneralRevisionManage)]
    [HttpPost("{id:guid}/checklist")]
    public async Task<ActionResult<GeneralRevisionReadinessDto>> LoadChecklist(Guid id, CancellationToken ct) => HandleResult(await completion.LoadChecklistAsync(id, ct));

    [RequirePermission(Permissions.GeneralRevisionManage)]
    [HttpPost("{id:guid}/checklist/{stepId:guid}/complete")]
    public async Task<ActionResult<GeneralRevisionReadinessDto>> CompleteStep(Guid id, Guid stepId, CompleteChecklistStepRequest request, CancellationToken ct) =>
        HandleResult(await completion.CompleteStepAsync(id, stepId, request, ct));

    [RequirePermission(Permissions.GeneralRevisionManage)]
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<GeneralRevisionDto>> Complete(Guid id, CancellationToken ct) => HandleResult(await completion.CompleteAsync(id, ct));

    [RequirePermission(Permissions.GeneralRevisionManage)]
    [HttpPost("{id:guid}/suspensions")]
    public async Task<ActionResult<GeneralRevisionDto>> Suspend(Guid id, SuspendGeneralRevisionRequest request, CancellationToken ct) =>
        HandleResult(await programmes.SuspendAsync(id, request, ct));

    [RequirePermission(Permissions.GeneralRevisionManage)]
    [HttpPost("{id:guid}/suspensions/{suspensionId:guid}/lift")]
    public async Task<ActionResult<GeneralRevisionDto>> Lift(Guid id, Guid suspensionId, LiftGeneralRevisionSuspensionRequest request, CancellationToken ct) =>
        HandleResult(await programmes.LiftSuspensionAsync(id, suspensionId, request, ct));

    [RequirePermission(Permissions.GeneralRevisionManage)]
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<GeneralRevisionDto>> Cancel(Guid id, CancelGeneralRevisionRequest request, CancellationToken ct) =>
        HandleResult(await programmes.CancelAsync(id, request, ct));
}

/// <summary>The general revision checklist template (configuration; docs/analysis/smv-preparation-general-revision.md §4.6, Q13).</summary>
[Route("api/general-revision/checklist-steps")]
public class GeneralRevisionChecklistStepsController(IGeneralRevisionCompletionService completion) : ApiControllerBase
{
    [RequirePermission(Permissions.GeneralRevisionView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChecklistStepDefinitionDto>>> List([FromQuery] bool inForceOnly, CancellationToken ct) =>
        HandleResult(await completion.ListStepDefinitionsAsync(inForceOnly, ct));

    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost]
    public async Task<ActionResult<ChecklistStepDefinitionDto>> Create(CreateChecklistStepDefinitionRequest request, CancellationToken ct) =>
        HandleResult(await completion.CreateStepDefinitionAsync(request, ct));

    [RequirePermission(Permissions.ConfigApprove)]
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ChecklistStepDefinitionDto>> Approve(Guid id, CancellationToken ct) => HandleResult(await completion.ApproveStepDefinitionAsync(id, ct));
}
