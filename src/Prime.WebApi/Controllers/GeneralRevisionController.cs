using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.GeneralRevision;

namespace Prime.WebApi.Controllers;

/// <summary>
/// Runs started before the general revision programme existed (L6-6), kept as history. Starting one from a list of units was
/// retired when the programme shipped (docs/analysis/smv-preparation-general-revision.md Q16): use
/// <c>/api/general-revision/programmes</c>.
/// </summary>
[Route("api/general-revision")]
public class GeneralRevisionController(IGeneralRevisionService generalRevisionService) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GeneralRevisionJobDto>> GetStatus(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await generalRevisionService.GetStatusAsync(id, cancellationToken));
}
