using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common;
using Prime.Application.Features.SmvSimulations;

namespace Prime.WebApi.Controllers;

/// <summary>Values under a proposed SMV, stored nowhere (docs/analysis/smv-preparation-general-revision.md §4.3).</summary>
[Route("api/smv/simulations")]
public class SmvSimulationsController(ISmvSimulationService simulations) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SmvSimulationRunDto>>> List([FromQuery] Guid? smvId, CancellationToken ct) =>
        HandleResult(await simulations.ListAsync(smvId, ct));

    [HttpPost]
    public async Task<ActionResult<SmvSimulationRunDto>> Start(StartSmvSimulationRequest request, CancellationToken ct) =>
        HandleResult(await simulations.StartAsync(request, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SmvSimulationRunDto>> Get(Guid id, CancellationToken ct) => HandleResult(await simulations.GetAsync(id, ct));

    [HttpGet("{id:guid}/results")]
    public async Task<ActionResult<PagedResult<SmvSimulationResultDto>>> Results(Guid id, [FromQuery] SmvSimulationResultSearchRequest request, CancellationToken ct) =>
        HandleResult(await simulations.SearchResultsAsync(id, request, ct));

    /// <summary>One unit valued and assessed under the SMV as of the date; nothing is stored.</summary>
    [HttpPost("~/api/rpus/{rpuId:guid}/simulation")]
    public async Task<ActionResult<SmvUnitSimulationDto>> SimulateUnit(Guid rpuId, [FromQuery] Guid smvId, [FromQuery] DateOnly asOf, CancellationToken ct) =>
        HandleResult(await simulations.SimulateUnitAsync(rpuId, smvId, asOf, ct));
}

/// <summary>Valuation tests of an SMV against accepted land sales (docs/analysis/smv-preparation-general-revision.md §4.3).</summary>
[Route("api/smv/valuation-tests")]
public class ValuationTestsController(IValuationTestService tests) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ValuationTestDto>>> List([FromQuery] Guid? smvId, CancellationToken ct) =>
        HandleResult(await tests.ListAsync(smvId, ct));

    [HttpPost]
    public async Task<ActionResult<ValuationTestDto>> Create(CreateValuationTestRequest request, CancellationToken ct) =>
        HandleResult(await tests.CreateAsync(request, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ValuationTestDto>> Get(Guid id, CancellationToken ct) => HandleResult(await tests.GetAsync(id, ct));

    [HttpGet("{id:guid}/sales")]
    public async Task<ActionResult<IReadOnlyList<ValuationTestSaleDto>>> Sales(Guid id, CancellationToken ct) => HandleResult(await tests.ListSalesAsync(id, ct));
}
