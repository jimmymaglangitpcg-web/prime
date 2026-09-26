using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common;
using Prime.Application.Features.SwornStatements;

namespace Prime.WebApi.Controllers;

/// <summary>The owner's sworn statement of market value (MRPAAO Att. 11; docs/analysis/mrpaao-forms-model.md §16).</summary>
[Route("api/sworn-statements")]
public class SwornStatementsController(ISwornStatementService statements) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SwornStatementSummaryDto>>> Search([FromQuery] SwornStatementSearchRequest request, CancellationToken ct) =>
        HandleResult(await statements.SearchAsync(request, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SwornStatementDto>> Get(Guid id, CancellationToken ct) => HandleResult(await statements.GetAsync(id, ct));

    [HttpGet("~/api/properties/{propertyId:guid}/sworn-statements")]
    public async Task<ActionResult<IReadOnlyList<SwornStatementDto>>> ForProperty(Guid propertyId, CancellationToken ct) =>
        HandleResult(await statements.ForPropertyAsync(propertyId, ct));

    [HttpPost]
    public async Task<ActionResult<SwornStatementDto>> Create(SaveSwornStatementRequest request, CancellationToken ct) =>
        HandleCreated(await statements.CreateAsync(request, ct), nameof(Get), s => new { id = s.Id });

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SwornStatementDto>> Update(Guid id, SaveSwornStatementRequest request, CancellationToken ct) =>
        HandleResult(await statements.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<SwornStatementDto>> AddItem(Guid id, AddSwornStatementItemRequest request, CancellationToken ct) =>
        HandleResult(await statements.AddItemAsync(id, request, ct));

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<SwornStatementDto>> RemoveItem(Guid id, Guid itemId, CancellationToken ct) =>
        HandleResult(await statements.RemoveItemAsync(id, itemId, ct));

    [HttpPost("{id:guid}/items/{itemId:guid}/link")]
    public async Task<ActionResult<SwornStatementDto>> LinkItem(Guid id, Guid itemId, LinkSwornStatementItemRequest request, CancellationToken ct) =>
        HandleResult(await statements.LinkItemAsync(id, itemId, request, ct));

    [HttpPost("{id:guid}/file")]
    public async Task<ActionResult<SwornStatementDto>> File(Guid id, FileSwornStatementRequest request, CancellationToken ct) =>
        HandleResult(await statements.FileAsync(id, request, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<SwornStatementDto>> Cancel(Guid id, CancelSwornStatementRequest request, CancellationToken ct) =>
        HandleResult(await statements.CancelAsync(id, request, ct));
}
