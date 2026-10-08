using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Valuation;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>Exchange rates and price indices for machinery (docs/analysis/valuation-foundation.md §4.6).</summary>
[Route("api")]
public class MachineryIndicesController(IMachineryIndexService indices) : ApiControllerBase
{
    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost("exchange-rates")]
    public async Task<ActionResult<ExchangeRateDto>> CreateExchangeRate(CreateExchangeRateRequest request, CancellationToken ct) =>
        HandleResult(await indices.CreateExchangeRateAsync(request, ct));

    [RequirePermission(Permissions.ConfigApprove)]
    [HttpPost("exchange-rates/{id:guid}/approve")]
    public async Task<ActionResult<ExchangeRateDto>> ApproveExchangeRate(Guid id, CancellationToken ct) =>
        HandleResult(await indices.ApproveExchangeRateAsync(id, ct));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("exchange-rates")]
    public async Task<ActionResult<IReadOnlyList<ExchangeRateDto>>> ListExchangeRates([FromQuery] string? currency, CancellationToken ct) =>
        HandleResult(await indices.ListExchangeRatesAsync(currency, ct));

    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost("price-indices")]
    public async Task<ActionResult<PriceIndexDto>> CreatePriceIndex(CreatePriceIndexRequest request, CancellationToken ct) =>
        HandleResult(await indices.CreatePriceIndexAsync(request, ct));

    [RequirePermission(Permissions.ConfigApprove)]
    [HttpPost("price-indices/{id:guid}/approve")]
    public async Task<ActionResult<PriceIndexDto>> ApprovePriceIndex(Guid id, CancellationToken ct) =>
        HandleResult(await indices.ApprovePriceIndexAsync(id, ct));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("price-indices")]
    public async Task<ActionResult<IReadOnlyList<PriceIndexDto>>> ListPriceIndices([FromQuery] string? series, CancellationToken ct) =>
        HandleResult(await indices.ListPriceIndicesAsync(series, ct));
}
