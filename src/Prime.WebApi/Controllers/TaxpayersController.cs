using Prime.Domain.Enums;
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

    /// <summary>Adds a party (owner, administrator, …, or unknown owner) — see ITaxpayerService.AddOwnerAsync.</summary>
    [HttpPost("~/api/properties/{propertyId:guid}/owners")]
    public async Task<ActionResult<PropertyOwnerDto>> AddOwner(Guid propertyId, [FromBody] AddOwnerBody body, CancellationToken cancellationToken)
    {
        var request = new AddPropertyOwnerRequest(propertyId, body.TaxpayerId, body.OwnershipTypeId, body.OwnershipPercentage, body.StartDate,
            body.Role ?? PropertyPartyRole.Owner);
        return HandleResult(await taxpayerService.AddOwnerAsync(request, cancellationToken));
    }

    [HttpGet("~/api/properties/{propertyId:guid}/owners")]
    public async Task<ActionResult<IReadOnlyList<PropertyOwnerDto>>> GetOwnershipHistory(Guid propertyId, CancellationToken cancellationToken) =>
        HandleResult(await taxpayerService.GetOwnershipHistoryAsync(propertyId, cancellationToken));

    /// <summary>Ends a party's current link to the property; history is kept.</summary>
    [HttpPost("~/api/property-owners/{propertyTaxpayerId:guid}/end")]
    public async Task<ActionResult<PropertyOwnerDto>> EndParty(Guid propertyTaxpayerId, [FromBody] EndPropertyPartyRequest request, CancellationToken cancellationToken) =>
        HandleResult(await taxpayerService.EndPartyAsync(propertyTaxpayerId, request, cancellationToken));

    /// <summary>Role defaults to Owner. TaxpayerId is omitted for an unknown owner; OwnershipTypeId is for owners only.</summary>
    public sealed record AddOwnerBody(Guid? TaxpayerId, Guid? OwnershipTypeId, decimal OwnershipPercentage, DateOnly StartDate, PropertyPartyRole? Role = null);
}
