using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.TaxDeclarations;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

// Explicit route: the default [controller] token would produce
// "api/TaxDeclarations", but CLAUDE.md §62 specifies "/api/tax-declarations".
[Route("api/tax-declarations")]
public class TaxDeclarationsController(ITaxDeclarationService taxDeclarationService) : ApiControllerBase
{
    [RequirePermission(Permissions.TdPrepare)]
    [HttpPost]
    public async Task<ActionResult<TaxDeclarationDto>> Create(CreateTaxDeclarationRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await taxDeclarationService.CreateAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaxDeclarationDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await taxDeclarationService.GetByIdAsync(id, cancellationToken));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("~/api/rpus/{rpuId:guid}/tax-declarations")]
    public async Task<ActionResult<IReadOnlyList<TaxDeclarationDto>>> ListByRpu(Guid rpuId, CancellationToken cancellationToken) =>
        HandleResult(await taxDeclarationService.ListByRpuAsync(rpuId, cancellationToken));

    // Lifecycle — docs/FORMS-REVISION-PLAN.md §5 A4.
    [RequirePermission(Permissions.TdPrepare)]
    [HttpPost("{id:guid}/submit-for-review")]
    public async Task<ActionResult<TaxDeclarationDto>> SubmitForReview(Guid id, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.SubmitForReviewAsync(id, ct));

    [RequirePermission(Permissions.TdApprove)]
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<TaxDeclarationDto>> Approve(Guid id, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.ApproveAsync(id, ct));

    [RequirePermission(Permissions.TdApprove)]
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<TaxDeclarationDto>> Reject(Guid id, [FromBody] TaxDeclarationReasonRequest request, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.RejectAsync(id, request.Reason, ct));

    /// <summary>Asks for an approved TD to be cancelled outright; a second user decides (workflow-security.md Q19).</summary>
    [RequirePermission(Permissions.TdPrepare)]
    [HttpPost("{id:guid}/cancellation-requests")]
    public async Task<ActionResult<TaxDeclarationDto>> RequestCancellation(Guid id, [FromBody] TaxDeclarationReasonRequest request, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.RequestCancellationAsync(id, request.Reason, ct));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("{id:guid}/cancellation-requests")]
    public async Task<ActionResult<IReadOnlyList<TaxDeclarationCancellationRequestDto>>> ListCancellationRequests(Guid id, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.ListCancellationRequestsAsync(id, ct));

    [RequirePermission(Permissions.TdApprove)]
    [HttpPost("cancellation-requests/{requestId:guid}/approve")]
    public async Task<ActionResult<TaxDeclarationDto>> ApproveCancellation(Guid requestId, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.ApproveCancellationAsync(requestId, ct));

    [RequirePermission(Permissions.TdApprove)]
    [HttpPost("cancellation-requests/{requestId:guid}/reject")]
    public async Task<ActionResult<TaxDeclarationDto>> RejectCancellation(Guid requestId, [FromBody] TaxDeclarationReasonRequest request, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.RejectCancellationAsync(requestId, request.Reason, ct));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("{id:guid}/annotations")]
    public async Task<ActionResult<IReadOnlyList<TaxDeclarationAnnotationDto>>> ListAnnotations(Guid id, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.ListAnnotationsAsync(id, ct));

    [RequirePermission(Permissions.TdPrepare)]
    [HttpPost("{id:guid}/annotations")]
    public async Task<ActionResult<TaxDeclarationAnnotationDto>> AddAnnotation(Guid id, AddTaxDeclarationAnnotationRequest request, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.AddAnnotationAsync(id, request, ct));

    [RequirePermission(Permissions.TdPrepare)]
    [HttpPost("~/api/tax-declaration-annotations/{annotationId:guid}/lift")]
    public async Task<ActionResult<TaxDeclarationAnnotationDto>> LiftAnnotation(Guid annotationId, LiftTaxDeclarationAnnotationRequest request, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.LiftAnnotationAsync(annotationId, request, ct));
}
