using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Prime.Application.Common;
using Prime.Application.Features.ContentPacks;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;
using Prime.WebApi.Security;

namespace Prime.WebApi.Controllers;

/// <summary>
/// LGU content packs (docs/analysis/lgu-content-pack.md; CLAUDE.md §118): list
/// the packs under the configured content root, preview one (a dry run that
/// writes nothing, C1) and import it (C2). Role gating (content.import)
/// arrives with Phase 12, as for the other configuration endpoints.
/// </summary>
[Route("api/content-packs")]
public class ContentPacksController(IContentPackService service) : ApiControllerBase
{
    [RequirePermission(Permissions.ConfigEdit)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContentPackInfo>>> List(CancellationToken ct) => HandleResult(await service.ListAsync(ct));

    /// <summary>
    /// Uploads a pack as a zip (manifest at its root or in one top-level folder); it replaces the pack of
    /// the same name in the content root, keeping the previous copy. Preview it next.
    /// </summary>
    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost("upload")]
    [EnableRateLimiting(RateLimiting.StrictPolicy)]
    [RequestSizeLimit(110 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 110 * 1024 * 1024)]
    public async Task<ActionResult<ContentPackInfo>> Upload(IFormFile file, CancellationToken ct)
    {
        if (CheckUpload(file, ".zip") is { IsSuccess: false } invalid)
        {
            return HandleResult(Result.Failure<ContentPackInfo>(invalid.Code!, invalid.Message!));
        }
        await using var stream = file.OpenReadStream();
        return HandleResult(await service.UploadAsync(stream, ct));
    }

    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost("{pack}/preview")]
    public async Task<ActionResult<ContentPackPreviewDto>> Preview(string pack, CancellationToken ct) => HandleResult(await service.PreviewAsync(pack, ct));

    /// <summary>Applies the pack previewed with <see cref="ImportContentPackRequest.Fingerprint"/>; 409 when its files changed since.</summary>
    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost("{pack}/import")]
    [EnableRateLimiting(RateLimiting.StrictPolicy)]
    public async Task<ActionResult<ContentImportResultDto>> Import(string pack, ImportContentPackRequest request, CancellationToken ct) =>
        HandleResult(await service.ImportAsync(pack, request, ct));
}

/// <summary>The record of applied content packs and what each created or changed (docs/analysis/lgu-content-pack.md §3.4).</summary>
[Route("api/content-imports")]
public class ContentImportsController(IContentPackService service) : ApiControllerBase
{
    [RequirePermission(Permissions.ConfigEdit)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<ContentImportDto>>> List([FromQuery] string? pack, [FromQuery] PagedRequest request, CancellationToken ct) =>
        HandleResult(await service.ListImportsAsync(pack, request, ct));

    [RequirePermission(Permissions.ConfigEdit)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContentImportDto>> Get(Guid id, CancellationToken ct) => HandleResult(await service.GetImportAsync(id, ct));

    [RequirePermission(Permissions.ConfigEdit)]
    [HttpGet("{id:guid}/items")]
    public async Task<ActionResult<PagedResult<ContentImportItemDto>>> Items(Guid id, [FromQuery] PagedRequest request, CancellationToken ct) =>
        HandleResult(await service.ListImportItemsAsync(id, request, ct));
}
