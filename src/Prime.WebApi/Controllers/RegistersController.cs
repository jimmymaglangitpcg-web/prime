using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Registers;

namespace Prime.WebApi.Controllers;

/// <summary>Dated runs of the MRPAAO registers (docs/analysis/mrpaao-forms-model.md §15); printed through the forms.</summary>
[Route("api/registers")]
public class RegistersController(IRegisterService registers) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RegisterRunDto>> Create(CreateRegisterRunRequest request, CancellationToken ct) =>
        HandleResult(await registers.CreateRunAsync(request, ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RegisterRunDto>>> List(CancellationToken ct) => HandleResult(await registers.ListRunsAsync(ct));
}
