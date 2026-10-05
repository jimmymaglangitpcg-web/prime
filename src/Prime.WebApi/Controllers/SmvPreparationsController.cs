using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Smv;

namespace Prime.WebApi.Controllers;

/// <summary>SMV preparation work files (docs/analysis/smv-preparation-general-revision.md §4.2).</summary>
[Route("api/smv/preparations")]
public class SmvPreparationsController(ISmvPreparationService preparations) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SmvPreparationSummaryDto>>> List(CancellationToken ct) => HandleResult(await preparations.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<SmvPreparationDto>> Create(CreateSmvPreparationRequest request, CancellationToken ct) =>
        HandleResult(await preparations.CreateAsync(request, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SmvPreparationDto>> Get(Guid id, CancellationToken ct) => HandleResult(await preparations.GetAsync(id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SmvPreparationDto>> Update(Guid id, UpdateSmvPreparationRequest request, CancellationToken ct) =>
        HandleResult(await preparations.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/consultations")]
    public async Task<ActionResult<SmvPreparationDto>> AddConsultation(Guid id, AddSmvConsultationRequest request, CancellationToken ct) =>
        HandleResult(await preparations.AddConsultationAsync(id, request, ct));

    [HttpPost("{id:guid}/events")]
    public async Task<ActionResult<SmvPreparationDto>> RecordEvent(Guid id, RecordSmvPreparationEventRequest request, CancellationToken ct) =>
        HandleResult(await preparations.RecordEventAsync(id, request, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<SmvPreparationDto>> Cancel(Guid id, CancelSmvPreparationRequest request, CancellationToken ct) =>
        HandleResult(await preparations.CancelAsync(id, request, ct));
}
