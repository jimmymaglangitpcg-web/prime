using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.TaxDeclarations;

namespace Prime.WebApi.Controllers;

// Explicit route: the default [controller] token would produce
// "api/TaxDeclarations", but CLAUDE.md §62 specifies "/api/tax-declarations".
[Route("api/tax-declarations")]
public class TaxDeclarationsController(ITaxDeclarationService taxDeclarationService) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TaxDeclarationDto>> Create(CreateTaxDeclarationRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await taxDeclarationService.CreateAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaxDeclarationDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await taxDeclarationService.GetByIdAsync(id, cancellationToken));

    [HttpGet("~/api/rpus/{rpuId:guid}/tax-declarations")]
    public async Task<ActionResult<IReadOnlyList<TaxDeclarationDto>>> ListByRpu(Guid rpuId, CancellationToken cancellationToken) =>
        HandleResult(await taxDeclarationService.ListByRpuAsync(rpuId, cancellationToken));

    // Lifecycle — docs/FORMS-REVISION-PLAN.md §5 A4.
    [HttpPost("{id:guid}/submit-for-review")]
    public async Task<ActionResult<TaxDeclarationDto>> SubmitForReview(Guid id, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.SubmitForReviewAsync(id, ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<TaxDeclarationDto>> Approve(Guid id, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.ApproveAsync(id, ct));

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<TaxDeclarationDto>> Reject(Guid id, [FromBody] TaxDeclarationReasonRequest request, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.RejectAsync(id, request.Reason, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<TaxDeclarationDto>> Cancel(Guid id, [FromBody] TaxDeclarationReasonRequest request, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.CancelAsync(id, request.Reason, ct));

    [HttpGet("{id:guid}/annotations")]
    public async Task<ActionResult<IReadOnlyList<TaxDeclarationAnnotationDto>>> ListAnnotations(Guid id, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.ListAnnotationsAsync(id, ct));

    [HttpPost("{id:guid}/annotations")]
    public async Task<ActionResult<TaxDeclarationAnnotationDto>> AddAnnotation(Guid id, AddTaxDeclarationAnnotationRequest request, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.AddAnnotationAsync(id, request, ct));

    [HttpPost("~/api/tax-declaration-annotations/{annotationId:guid}/lift")]
    public async Task<ActionResult<TaxDeclarationAnnotationDto>> LiftAnnotation(Guid annotationId, LiftTaxDeclarationAnnotationRequest request, CancellationToken ct) =>
        HandleResult(await taxDeclarationService.LiftAnnotationAsync(annotationId, request, ct));
}
