using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common.Security;
using Prime.Application.Features.ReportConfiguration;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>Report row maps (docs/analysis/reporting.md §10, Q15): configuration under maker-checker.</summary>
[Route("api/report-row-maps")]
public class ReportRowMapsController(IReportRowMapService maps) : ApiControllerBase
{
    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportRowMapDto>>> List(CancellationToken ct) => HandleResult(await maps.ListAsync(ct));

    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost]
    public async Task<ActionResult<ReportRowMapDto>> Create(CreateReportRowMapRequest request, CancellationToken ct) =>
        HandleResult(await maps.CreateAsync(request, ct));

    [RequirePermission(Permissions.ConfigApprove)]
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ReportRowMapDto>> Approve(Guid id, CancellationToken ct) => HandleResult(await maps.ApproveAsync(id, ct));
}

/// <summary>Dated system parameters (docs/analysis/reporting.md §10, Q16): configuration under maker-checker.</summary>
[Route("api/system-parameters")]
public class SystemParametersController(ISystemParameterService parameters) : ApiControllerBase
{
    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SystemParameterDto>>> List(CancellationToken ct) => HandleResult(await parameters.ListAsync(ct));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("catalog")]
    public ActionResult<IReadOnlyList<SystemParameterDefinition>> Catalog() => Ok(parameters.Catalog());

    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost]
    public async Task<ActionResult<SystemParameterDto>> Create(CreateSystemParameterRequest request, CancellationToken ct) =>
        HandleResult(await parameters.CreateAsync(request, ct));

    [RequirePermission(Permissions.ConfigApprove)]
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<SystemParameterDto>> Approve(Guid id, CancellationToken ct) => HandleResult(await parameters.ApproveAsync(id, ct));
}
