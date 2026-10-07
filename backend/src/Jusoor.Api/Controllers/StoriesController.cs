using Jusoor.Application.Common.Security;
using Jusoor.Application.Stories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

/// <summary>
/// Internal read endpoints for ingested Stories. Introduced in Slice 8 as
/// public, unauthenticated endpoints; since Slice 17 they are NEWSROOM-ONLY
/// (policy CanViewIncomingStories, see the attribute below) because nothing
/// may reach the public without human approval (spec §03) — the public
/// website reads the published-only /api/v1/news instead. The query handlers
/// still refuse to return a non-Relevant Story regardless of caller.
/// </summary>
[ApiController]
[Route("api/v1/stories")]
// Slice 17: incoming stories are internal newsroom material (spec §03 — nothing
// publishes without human approval). The public website reads /api/v1/news.
[Authorize(Policy = AuthorizationPolicies.CanViewIncomingStories)]
public class StoriesController : ControllerBase
{
    private readonly ISender _mediator;

    public StoriesController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHomepageStories(
        [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetHomepageStoriesQuery(page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{storyId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStoryById(Guid storyId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetStoryByIdQuery(storyId), cancellationToken);

        return result is null
            ? NotFound(new { message = "Story not found." })
            : Ok(result);
    }
}
