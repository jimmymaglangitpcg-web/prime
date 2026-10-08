using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common;
using Prime.Application.Features.SwornStatements;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>The owner's sworn statement of market value (MRPAAO Att. 11; docs/analysis/mrpaao-forms-model.md §16).</summary>
[Route("api/sworn-statements")]
public class SwornStatementsController(ISwornStatementService statements) : ApiControllerBase
{
    [RequirePermission(Permissions.PropertyView)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<SwornStatementSummaryDto>>> Search([FromQuery] SwornStatementSearchRequest request, CancellationToken ct) =>
        HandleResult(await statements.SearchAsync(request, ct));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SwornStatementDto>> Get(Guid id, CancellationToken ct) => HandleResult(await statements.GetAsync(id, ct));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("~/api/properties/{propertyId:guid}/sworn-statements")]
    public async Task<ActionResult<IReadOnlyList<SwornStatementDto>>> ForProperty(Guid propertyId, CancellationToken ct) =>
        HandleResult(await statements.ForPropertyAsync(propertyId, ct));

    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPost]
    public async Task<ActionResult<SwornStatementDto>> Create(SaveSwornStatementRequest request, CancellationToken ct) =>
        HandleCreated(await statements.CreateAsync(request, ct), nameof(Get), s => new { id = s.Id });

    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SwornStatementDto>> Update(Guid id, SaveSwornStatementRequest request, CancellationToken ct) =>
        HandleResult(await statements.UpdateAsync(id, request, ct));

    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<SwornStatementDto>> AddItem(Guid id, AddSwornStatementItemRequest request, CancellationToken ct) =>
        HandleResult(await statements.AddItemAsync(id, request, ct));

    [RequirePermission(Permissions.PropertyEdit)]
    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<SwornStatementDto>> RemoveItem(Guid id, Guid itemId, CancellationToken ct) =>
        HandleResult(await statements.RemoveItemAsync(id, itemId, ct));

    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPost("{id:guid}/items/{itemId:guid}/link")]
    public async Task<ActionResult<SwornStatementDto>> LinkItem(Guid id, Guid itemId, LinkSwornStatementItemRequest request, CancellationToken ct) =>
        HandleResult(await statements.LinkItemAsync(id, itemId, request, ct));

    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPost("{id:guid}/file")]
    public async Task<ActionResult<SwornStatementDto>> File(Guid id, FileSwornStatementRequest request, CancellationToken ct) =>
        HandleResult(await statements.FileAsync(id, request, ct));

    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<SwornStatementDto>> Cancel(Guid id, CancelSwornStatementRequest request, CancellationToken ct) =>
        HandleResult(await statements.CancelAsync(id, request, ct));
}
