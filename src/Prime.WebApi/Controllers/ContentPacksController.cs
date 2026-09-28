using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.ContentPacks;

namespace Prime.WebApi.Controllers;

/// <summary>
/// LGU content packs (docs/analysis/lgu-content-pack.md; CLAUDE.md §118).
/// Step C1: list the packs under the configured content root and preview one
/// (a dry run that writes nothing). Import comes in C2. Role gating
/// (content.import) arrives with Phase 12, as for the other configuration endpoints.
/// </summary>
[Route("api/content-packs")]
public class ContentPacksController(IContentPackService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContentPackInfo>>> List(CancellationToken ct) => HandleResult(await service.ListAsync(ct));

    [HttpPost("{pack}/preview")]
    public async Task<ActionResult<ContentPackPreviewDto>> Preview(string pack, CancellationToken ct) => HandleResult(await service.PreviewAsync(pack, ct));
}
