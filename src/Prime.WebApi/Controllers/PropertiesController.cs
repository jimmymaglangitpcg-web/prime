using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common;
using Prime.Application.Features.Properties;

namespace Prime.WebApi.Controllers;

public class PropertiesController(IPropertyService propertyService) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PropertyDto>> Create(CreatePropertyRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await propertyService.CreateAsync(request, cancellationToken), nameof(GetProfile), dto => new { id = dto.Id });

    /// <summary>CLAUDE.md §50 Property Profile.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PropertyProfileDto>> GetProfile(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await propertyService.GetProfileAsync(id, cancellationToken));

    /// <summary>CLAUDE.md §56 global-ish property search (PIN, lot/title/survey/tax-map number).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PropertyDto>>> Search([FromQuery] PropertySearchRequest request, CancellationToken cancellationToken) =>
        HandleResult(await propertyService.SearchAsync(request, cancellationToken));
}
