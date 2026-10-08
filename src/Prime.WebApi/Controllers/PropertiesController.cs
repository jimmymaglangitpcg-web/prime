using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common;
using Prime.Application.Features.Properties;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

public class PropertiesController(IPropertyService propertyService) : ApiControllerBase
{
    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPost]
    public async Task<ActionResult<PropertyDto>> Create(CreatePropertyRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await propertyService.CreateAsync(request, cancellationToken), nameof(GetProfile), dto => new { id = dto.Id });

    /// <summary>CLAUDE.md §50 Property Profile.</summary>
    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PropertyProfileDto>> GetProfile(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await propertyService.GetProfileAsync(id, cancellationToken));

    /// <summary>CLAUDE.md §56 global-ish property search (PIN, lot/title/survey/tax-map number).</summary>
    [RequirePermission(Permissions.PropertyView)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<PropertyDto>>> Search([FromQuery] PropertySearchRequest request, CancellationToken cancellationToken) =>
        HandleResult(await propertyService.SearchAsync(request, cancellationToken));
}
