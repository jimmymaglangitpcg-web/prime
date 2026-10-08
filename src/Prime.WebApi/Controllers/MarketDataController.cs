using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Prime.Application.Common;
using Prime.Application.Features.MarketData;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;
using Prime.WebApi.Security;

namespace Prime.WebApi.Controllers;

/// <summary>
/// Market data for the SMV: transactions, abstracts of building permits and machinery registrations, import, reports
/// (docs/analysis/smv-preparation-general-revision.md §4.1). Prices and parties are personal data; reads are limited to
/// the office's jurisdiction. A dedicated permission comes with Phase 12 (CLAUDE.md §99).
/// </summary>
[Route("api/market-data")]
public class MarketDataController(
    IMarketTransactionService transactions, IMarketDataAbstractService abstracts, IMarketDataImportService import, IMarketDataReportService reports) : ApiControllerBase
{
    [RequirePermission(Permissions.MarketView)]
    [HttpGet("transactions")]
    public async Task<ActionResult<PagedResult<MarketTransactionDto>>> SearchTransactions([FromQuery] MarketTransactionSearchRequest request, CancellationToken ct) =>
        HandleResult(await transactions.SearchAsync(request, ct));

    [RequirePermission(Permissions.MarketView)]
    [HttpGet("transactions/{id:guid}")]
    public async Task<ActionResult<MarketTransactionDto>> GetTransaction(Guid id, CancellationToken ct) => HandleResult(await transactions.GetAsync(id, ct));

    [RequirePermission(Permissions.MarketEdit)]
    [HttpPost("transactions")]
    public async Task<ActionResult<MarketTransactionDto>> CreateTransaction(SaveMarketTransactionRequest request, CancellationToken ct) =>
        HandleResult(await transactions.CreateAsync(request, ct));

    [RequirePermission(Permissions.MarketEdit)]
    [HttpPut("transactions/{id:guid}")]
    public async Task<ActionResult<MarketTransactionDto>> UpdateTransaction(Guid id, SaveMarketTransactionRequest request, CancellationToken ct) =>
        HandleResult(await transactions.UpdateAsync(id, request, ct));

    [RequirePermission(Permissions.MarketEdit)]
    [HttpPost("transactions/{id:guid}/review")]
    public async Task<ActionResult<MarketTransactionDto>> ReviewTransaction(Guid id, ReviewMarketTransactionRequest request, CancellationToken ct) =>
        HandleResult(await transactions.ReviewAsync(id, request, ct));

    [RequirePermission(Permissions.MarketEdit)]
    [HttpPost("transactions/{id:guid}/cancel")]
    public async Task<ActionResult<MarketTransactionDto>> CancelTransaction(Guid id, CancelMarketDataRequest request, CancellationToken ct) =>
        HandleResult(await transactions.CancelAsync(id, request, ct));

    /// <summary>Validates a CSV file (multipart field <c>file</c>); nothing is saved.</summary>
    [RequirePermission(Permissions.MarketEdit)]
    [HttpPost("transactions/import/preview")]
    [EnableRateLimiting(RateLimiting.StrictPolicy)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<MarketImportResultDto>> PreviewImport(IFormFile file, CancellationToken ct) =>
        CheckUpload(file, ".csv") is { IsSuccess: false } invalid
            ? HandleResult(Result.Failure<MarketImportResultDto>(invalid.Code!, invalid.Message!))
            : HandleResult(await import.PreviewAsync(await ReadAsync(file, ct), ct));

    /// <summary>Imports the file previewed with <paramref name="fingerprint"/>; refused if any row is invalid or the file changed.</summary>
    [RequirePermission(Permissions.MarketEdit)]
    [HttpPost("transactions/import")]
    [EnableRateLimiting(RateLimiting.StrictPolicy)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<MarketImportResultDto>> Import(IFormFile file, [FromQuery] string fingerprint, CancellationToken ct) =>
        CheckUpload(file, ".csv") is { IsSuccess: false } invalid
            ? HandleResult(Result.Failure<MarketImportResultDto>(invalid.Code!, invalid.Message!))
            : HandleResult(await import.ImportAsync(await ReadAsync(file, ct), fingerprint, ct));

    [RequirePermission(Permissions.MarketView)]
    [HttpGet("building-permits")]
    public async Task<ActionResult<PagedResult<BuildingPermitDto>>> SearchPermits([FromQuery] AbstractSearchRequest request, CancellationToken ct) =>
        HandleResult(await abstracts.SearchPermitsAsync(request, ct));

    [RequirePermission(Permissions.MarketEdit)]
    [HttpPost("building-permits")]
    public async Task<ActionResult<BuildingPermitDto>> CreatePermit(SaveBuildingPermitRequest request, CancellationToken ct) =>
        HandleResult(await abstracts.CreatePermitAsync(request, ct));

    [RequirePermission(Permissions.MarketEdit)]
    [HttpPut("building-permits/{id:guid}")]
    public async Task<ActionResult<BuildingPermitDto>> UpdatePermit(Guid id, SaveBuildingPermitRequest request, CancellationToken ct) =>
        HandleResult(await abstracts.UpdatePermitAsync(id, request, ct));

    [RequirePermission(Permissions.MarketEdit)]
    [HttpPost("building-permits/{id:guid}/link")]
    public async Task<ActionResult<BuildingPermitDto>> LinkPermit(Guid id, LinkAbstractRequest request, CancellationToken ct) =>
        HandleResult(await abstracts.LinkPermitAsync(id, request, ct));

    [RequirePermission(Permissions.MarketEdit)]
    [HttpPost("building-permits/{id:guid}/cancel")]
    public async Task<ActionResult<BuildingPermitDto>> CancelPermit(Guid id, CancelMarketDataRequest request, CancellationToken ct) =>
        HandleResult(await abstracts.CancelPermitAsync(id, request, ct));

    [RequirePermission(Permissions.MarketView)]
    [HttpGet("machinery-registrations")]
    public async Task<ActionResult<PagedResult<MachineryRegistrationDto>>> SearchRegistrations([FromQuery] AbstractSearchRequest request, CancellationToken ct) =>
        HandleResult(await abstracts.SearchRegistrationsAsync(request, ct));

    [RequirePermission(Permissions.MarketEdit)]
    [HttpPost("machinery-registrations")]
    public async Task<ActionResult<MachineryRegistrationDto>> CreateRegistration(SaveMachineryRegistrationRequest request, CancellationToken ct) =>
        HandleResult(await abstracts.CreateRegistrationAsync(request, ct));

    [RequirePermission(Permissions.MarketEdit)]
    [HttpPut("machinery-registrations/{id:guid}")]
    public async Task<ActionResult<MachineryRegistrationDto>> UpdateRegistration(Guid id, SaveMachineryRegistrationRequest request, CancellationToken ct) =>
        HandleResult(await abstracts.UpdateRegistrationAsync(id, request, ct));

    [RequirePermission(Permissions.MarketEdit)]
    [HttpPost("machinery-registrations/{id:guid}/link")]
    public async Task<ActionResult<MachineryRegistrationDto>> LinkRegistration(Guid id, LinkAbstractRequest request, CancellationToken ct) =>
        HandleResult(await abstracts.LinkRegistrationAsync(id, request, ct));

    [RequirePermission(Permissions.MarketEdit)]
    [HttpPost("machinery-registrations/{id:guid}/cancel")]
    public async Task<ActionResult<MachineryRegistrationDto>> CancelRegistration(Guid id, CancelMarketDataRequest request, CancellationToken ct) =>
        HandleResult(await abstracts.CancelRegistrationAsync(id, request, ct));

    [RequirePermission(Permissions.MarketView)]
    [HttpGet("reports")]
    public async Task<ActionResult<IReadOnlyList<MarketDataReportRunDto>>> ListReports(CancellationToken ct) => HandleResult(await reports.ListAsync(ct));

    [RequirePermission(Permissions.MarketEdit)]
    [HttpPost("reports")]
    public async Task<ActionResult<MarketDataReportRunDto>> CreateReport(CreateMarketDataReportRequest request, CancellationToken ct) =>
        HandleResult(await reports.CreateAsync(request, ct));

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
