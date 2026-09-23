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
}
