using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Valuation;

namespace Prime.WebApi.Controllers;

/// <summary>Exchange rates and price indices for machinery (docs/analysis/valuation-foundation.md §4.6).</summary>
[Route("api")]
public class MachineryIndicesController(IMachineryIndexService indices) : ApiControllerBase
{
    [HttpPost("exchange-rates")]
    public async Task<ActionResult<ExchangeRateDto>> CreateExchangeRate(CreateExchangeRateRequest request, CancellationToken ct) =>
        HandleResult(await indices.CreateExchangeRateAsync(request, ct));

    [HttpPost("exchange-rates/{id:guid}/approve")]
    public async Task<ActionResult<ExchangeRateDto>> ApproveExchangeRate(Guid id, CancellationToken ct) =>
        HandleResult(await indices.ApproveExchangeRateAsync(id, ct));

    [HttpGet("exchange-rates")]
    public async Task<ActionResult<IReadOnlyList<ExchangeRateDto>>> ListExchangeRates([FromQuery] string? currency, CancellationToken ct) =>
        HandleResult(await indices.ListExchangeRatesAsync(currency, ct));

    [HttpPost("price-indices")]
    public async Task<ActionResult<PriceIndexDto>> CreatePriceIndex(CreatePriceIndexRequest request, CancellationToken ct) =>
        HandleResult(await indices.CreatePriceIndexAsync(request, ct));

    [HttpPost("price-indices/{id:guid}/approve")]
    public async Task<ActionResult<PriceIndexDto>> ApprovePriceIndex(Guid id, CancellationToken ct) =>
        HandleResult(await indices.ApprovePriceIndexAsync(id, ct));

    [HttpGet("price-indices")]
    public async Task<ActionResult<IReadOnlyList<PriceIndexDto>>> ListPriceIndices([FromQuery] string? series, CancellationToken ct) =>
        HandleResult(await indices.ListPriceIndicesAsync(series, ct));
}
