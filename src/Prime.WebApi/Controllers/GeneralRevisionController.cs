using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.GeneralRevision;

namespace Prime.WebApi.Controllers;

[Route("api/general-revision")]
public class GeneralRevisionController(IGeneralRevisionService generalRevisionService) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<GeneralRevisionJobDto>> Start(StartGeneralRevisionRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await generalRevisionService.StartAsync(request, cancellationToken), nameof(GetStatus), dto => new { id = dto.Id });

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GeneralRevisionJobDto>> GetStatus(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await generalRevisionService.GetStatusAsync(id, cancellationToken));
}
