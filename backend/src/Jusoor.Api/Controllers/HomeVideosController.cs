using Jusoor.Application.Common.Security;
using Jusoor.Application.Editorial;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

/// <summary>
/// Curation of the homepage video hero: which published videos appear, and in what order.
/// Senior/Managing/Editor-in-Chief only (same roles that manage publication state).
/// </summary>
[ApiController]
[Route("api/v1/cms/home-videos")]
[Authorize(Policy = AuthorizationPolicies.CanManageEditorialPublicationState)]
public sealed class HomeVideosController : ControllerBase
{
    private readonly ISender _sender;
    public HomeVideosController(ISender sender) => _sender = sender;

    public sealed record SetHomeVideosRequest(Guid[] ArticleIds);

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetHomeVideoCandidatesQuery(), cancellationToken));

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Set(SetHomeVideosRequest request, CancellationToken cancellationToken)
    {
        var outcome = await _sender.Send(new SetHomeVideosCommand(request.ArticleIds ?? Array.Empty<Guid>()), cancellationToken);
        return outcome == SetHomeVideosOutcome.Success
            ? NoContent()
            : BadRequest(new { message = "كل فيديو مختار لازم يكون خبرًا منشورًا وله فيديو جاهز." });
    }
}
