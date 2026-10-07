using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Common.Security;
using Jusoor.Application.Review;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

/// <summary>
/// The human review queue — closing Phase 1's NeedsHumanReview gap (Slice 5
/// introduced the status, nothing since could act on it). Authenticated and
/// role-gated, unlike StoriesController: this exposes unpublished content
/// (a Story that hasn't cleared relevance review is by definition not
/// public) and a mutating editorial action, so both read endpoints and the
/// write endpoint sit behind [Authorize] — see AuthorizationPolicies for
/// exactly which roles that resolves to today.
/// </summary>
[ApiController]
[Route("api/v1/review")]
[Authorize(Policy = AuthorizationPolicies.CanResolveHumanReview)]
public class ReviewController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;

    public ReviewController(ISender mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    public record ResolveReviewRequest(bool IsRelevant, string Reasoning);

    [HttpGet("queue")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueue([FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetReviewQueueQuery(page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("queue/{storyId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQueueItem(Guid storyId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetReviewQueueItemQuery(storyId), cancellationToken);

        return result is null
            ? NotFound(new { message = "Story not found or not awaiting human review." })
            : Ok(result);
    }

    [HttpPost("queue/{storyId:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResolveQueueItem(Guid storyId, ResolveReviewRequest request, CancellationToken cancellationToken)
    {
        // ReviewedByUserId comes from the authenticated principal, never
        // from the request body — see ResolveReviewCommand's own comment
        // on why that matters for attribution integrity. [Authorize] above
        // already guarantees ICurrentUserService.UserId is set here; the
        // null-check is defense in depth, not an expected path.
        var reviewerId = _currentUser.UserId;
        if (reviewerId is null)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(
            new ResolveReviewCommand(storyId, request.IsRelevant, request.Reasoning, reviewerId),
            cancellationToken);

        return result.Outcome switch
        {
            ResolveReviewOutcome.Resolved => Ok(new { status = result.NewStatus }),
            ResolveReviewOutcome.StoryNotFound => NotFound(new { message = "Story not found." }),
            ResolveReviewOutcome.StoryNotInReview => Conflict(new
            {
                message = "This story is no longer awaiting human review — someone may have already resolved it.",
                currentStatus = result.NewStatus
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
