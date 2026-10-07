using Jusoor.Application.Common.Security;
using Jusoor.Application.Ingestion;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

[ApiController]
[Route("api/v1/sources")]
[Authorize(Policy = AuthorizationPolicies.CanManageSources)] // Slice 17: ManagingEditor / EditorInChief / SystemAdmin (approved matrix)
public class IngestionController : ControllerBase
{
    private readonly ISender _mediator;

    public IngestionController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("{sourceId:guid}/ingest")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ingest(Guid sourceId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new IngestSourceCommand(sourceId), cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}
