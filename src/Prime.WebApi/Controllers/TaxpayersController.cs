using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common;
using Prime.Application.Features.Properties;
using Prime.Application.Features.Taxpayers;

namespace Prime.WebApi.Controllers;

public class TaxpayersController(ITaxpayerService taxpayerService) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TaxpayerDto>> Create(CreateTaxpayerRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await taxpayerService.CreateAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaxpayerDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await taxpayerService.GetByIdAsync(id, cancellationToken));

    [HttpGet]
    public async Task<ActionResult<PagedResult<TaxpayerDto>>> Search([FromQuery] TaxpayerSearchRequest request, CancellationToken cancellationToken) =>
        HandleResult(await taxpayerService.SearchAsync(request, cancellationToken));

    /// <summary>Adds an owner to a property — supports co-ownership; see ITaxpayerService.AddOwnerAsync.</summary>
    [HttpPost("~/api/properties/{propertyId:guid}/owners")]
    public async Task<ActionResult<PropertyOwnerDto>> AddOwner(Guid propertyId, [FromBody] AddOwnerBody body, CancellationToken cancellationToken)
    {
        var request = new AddPropertyOwnerRequest(propertyId, body.TaxpayerId, body.OwnershipTypeId, body.OwnershipPercentage, body.StartDate);
        return HandleResult(await taxpayerService.AddOwnerAsync(request, cancellationToken));
    }

    [HttpGet("~/api/properties/{propertyId:guid}/owners")]
    public async Task<ActionResult<IReadOnlyList<PropertyOwnerDto>>> GetOwnershipHistory(Guid propertyId, CancellationToken cancellationToken) =>
        HandleResult(await taxpayerService.GetOwnershipHistoryAsync(propertyId, cancellationToken));

    public sealed record AddOwnerBody(Guid TaxpayerId, Guid OwnershipTypeId, decimal OwnershipPercentage, DateOnly StartDate);
}
