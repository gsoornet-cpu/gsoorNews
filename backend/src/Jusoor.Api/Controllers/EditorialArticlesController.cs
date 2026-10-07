using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Common.Security;
using Jusoor.Application.Editorial;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

/// <summary>
/// CMS endpoints for newsroom-authored articles. Role gates are named
/// policies (coarse: who may reach the endpoint); "own items only" rules are
/// enforced in the handlers via EditorialArticleAccess. The acting user's id
/// and roles always come from the authenticated principal, never the body.
/// </summary>
[ApiController]
[Route("api/v1/cms/articles")]
[Authorize(Policy = AuthorizationPolicies.CanAccessCms)]
public class EditorialArticlesController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;

    public EditorialArticlesController(ISender mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <param name="EditReason">Optional note recorded with the revision when a PUBLISHED article is edited (decision D4).</param>
    /// <param name="Body">HTML from the CMS editor. It is sanitized server-side (decision D2) — only the allow-listed tags/attributes survive.</param>
    public record SaveArticleRequest(string Title, string? Summary, string Body, Guid? CountryId, Guid? CityId,
        string? EditReason = null, string[]? PresentationDesks = null, Guid? FeaturedVideoMediaAssetId = null,
        string? CoverImageUrl = null, string? AuthorName = null);
    public record SchedulePublicationRequest(DateTimeOffset ScheduledPublishAtUtc, string? Reason = null);

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page, [FromQuery] int pageSize, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        EditorialArticleStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<EditorialArticleStatus>(status, ignoreCase: true, out var s) || !Enum.IsDefined(s))
            {
                return BadRequest(new { message = "status must be a defined editorial article status." });
            }

            parsedStatus = s;
        }

        var result = await _mediator.Send(
            new ListEditorialArticlesQuery(page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize, parsedStatus, userId, _currentUser.Roles),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new GetEditorialArticleQuery(id, userId, _currentUser.Roles), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanCreateEditorialArticle)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(SaveArticleRequest request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(
            new CreateEditorialArticleCommand(
                request.Title, request.Summary, request.Body, request.CountryId, request.CityId, userId, _currentUser.Roles,
                request.PresentationDesks, request.FeaturedVideoMediaAssetId, request.CoverImageUrl, request.AuthorName),
            cancellationToken);

        return result.Outcome == EditorialOutcome.Success
            ? CreatedAtAction(nameof(Get), new { id = result.Article!.Id }, result.Article)
            : ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanCreateEditorialArticle)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, SaveArticleRequest request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(
            new UpdateEditorialArticleCommand(
                id, request.Title, request.Summary, request.Body, request.CountryId, request.CityId, userId, _currentUser.Roles,
                request.EditReason, request.PresentationDesks, request.FeaturedVideoMediaAssetId, request.CoverImageUrl, request.AuthorName),
            cancellationToken);

        return ToActionResult(result);
    }

    public record RevisionRequest(string? Reason);

    public record RetractionRequest(string PublicNotice, string InternalReason);

    public record TransitionReasonRequest(string? Reason);

    /// <summary>Draft → InReview. The author (or an editor on their behalf) hands the article over for review.</summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = AuthorizationPolicies.CanCreateEditorialArticle)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new SubmitEditorialArticleCommand(id, userId, _currentUser.Roles), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>InReview/Approved → Draft, with a mandatory reason shown to the author.</summary>
    [HttpPost("{id:guid}/request-revision")]
    [Authorize(Policy = AuthorizationPolicies.CanRequestEditorialRevision)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestRevision(Guid id, RevisionRequest request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(
            new RequestEditorialRevisionCommand(id, request.Reason ?? string.Empty, userId, _currentUser.Roles), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>InReview → Approved.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = AuthorizationPolicies.CanApproveEditorialArticle)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new ApproveEditorialArticleCommand(id, userId, _currentUser.Roles), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = AuthorizationPolicies.CanPublishEditorialArticle)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(Guid id, [FromBody] TransitionReasonRequest? request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new PublishEditorialArticleCommand(id, userId, _currentUser.Roles, request?.Reason), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Schedules an approved article; scheduling cannot bypass review.</summary>
    [HttpPost("{id:guid}/schedule")]
    [Authorize(Policy = AuthorizationPolicies.CanPublishEditorialArticle)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Schedule(Guid id, SchedulePublicationRequest request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _mediator.Send(new ScheduleEditorialPublicationCommand(
            id, request.ScheduledPublishAtUtc, userId, _currentUser.Roles, request.Reason), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Cancels a pending schedule and returns the article to Approved.</summary>
    [HttpPost("{id:guid}/cancel-schedule")]
    [Authorize(Policy = AuthorizationPolicies.CanPublishEditorialArticle)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelSchedule(Guid id, [FromBody] TransitionReasonRequest? request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _mediator.Send(new CancelScheduledEditorialPublicationCommand(
            id, userId, _currentUser.Roles, request?.Reason), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id:guid}/unpublish")]
    [Authorize(Policy = AuthorizationPolicies.CanPublishEditorialArticle)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unpublish(Guid id, [FromBody] TransitionReasonRequest? request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new UnpublishEditorialArticleCommand(id, userId, _currentUser.Roles, request?.Reason), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Published → Retracted. The public notice and internal audit reason are separate.</summary>
    [HttpPost("{id:guid}/retract")]
    [Authorize(Policy = AuthorizationPolicies.CanManageEditorialPublicationState)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retract(Guid id, RetractionRequest request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _mediator.Send(new RetractEditorialArticleCommand(
            id, request.PublicNotice ?? string.Empty, request.InternalReason ?? string.Empty, userId, _currentUser.Roles), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Published → Archived; the article remains readable by its public URL.</summary>
    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = AuthorizationPolicies.CanManageEditorialPublicationState)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Archive(Guid id, TransitionReasonRequest request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _mediator.Send(new ArchiveEditorialArticleCommand(
            id, request.Reason, userId, _currentUser.Roles), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Archived → Published.</summary>
    [HttpPost("{id:guid}/restore-archive")]
    [Authorize(Policy = AuthorizationPolicies.CanManageEditorialPublicationState)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RestoreArchive(Guid id, TransitionReasonRequest request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _mediator.Send(new RestoreEditorialArticleFromArchiveCommand(
            id, request.Reason, userId, _currentUser.Roles), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Retracted → Draft. Only the Editor-in-Chief may reinstate.</summary>
    [HttpPost("{id:guid}/reinstate")]
    [Authorize(Policy = AuthorizationPolicies.CanReinstateRetractedEditorialArticle)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reinstate(Guid id, TransitionReasonRequest request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _mediator.Send(new ReinstateRetractedEditorialArticleCommand(
            id, request.Reason ?? string.Empty, userId, _currentUser.Roles), cancellationToken);
        return ToActionResult(result);
    }

    // ---------- Slice 20 (decision D4): history and corrections ----------

    /// <summary>Workflow transitions, revisions (metadata) and corrections of one article, oldest first.</summary>
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new GetEditorialArticleHistoryQuery(id, userId, _currentUser.Roles), cancellationToken);
        return result.Outcome == EditorialOutcome.Success
            ? Ok(result.History)
            : NotFound(new { message = "Article not found." });
    }

    /// <summary>The article exactly as it read before revision <paramref name="number"/>'s edit.</summary>
    [HttpGet("{id:guid}/revisions/{number:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revision(Guid id, int number, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new GetEditorialArticleRevisionQuery(id, number, userId, _currentUser.Roles), cancellationToken);
        return result.Outcome == EditorialOutcome.Success
            ? Ok(result.Revision)
            : NotFound(new { message = "Revision not found." });
    }

    /// <param name="Kind">Correction, Clarification, EditorNote or Update. Historical Notice values remain readable.</param>
    /// <param name="IsMajor">Show the note above the article text instead of below it.</param>
    public record IssueCorrectionRequest(string Kind, string Note, bool IsMajor = false);

    /// <summary>Attaches an append-only public correction/notice to a PUBLISHED article.</summary>
    [HttpPost("{id:guid}/corrections")]
    [Authorize(Policy = AuthorizationPolicies.CanIssueEditorialCorrection)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IssueCorrection(Guid id, IssueCorrectionRequest request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        if (!Enum.TryParse<CorrectionKind>(request.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            return BadRequest(new { message = "kind must be Correction, Clarification, EditorNote or Update." });
        }

        if (kind == CorrectionKind.Notice)
        {
            return BadRequest(new { message = "Notice is retained for historical records; new corrections must use Correction, Clarification, EditorNote or Update." });
        }

        var result = await _mediator.Send(
            new IssueEditorialCorrectionCommand(id, kind, request.Note ?? string.Empty, request.IsMajor, userId, _currentUser.Roles),
            cancellationToken);

        return result.Outcome switch
        {
            EditorialOutcome.Success => StatusCode(StatusCodes.Status201Created, result.Correction),
            EditorialOutcome.NotFound => NotFound(new { message = "Article not found." }),
            EditorialOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to issue a correction on this article." }),
            EditorialOutcome.Conflict => Conflict(new { message = "A correction can only be attached to a published or archived article." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private IActionResult ToActionResult(EditorialArticleResult result) => result.Outcome switch
    {
        EditorialOutcome.Success => Ok(result.Article),
        EditorialOutcome.NotFound => NotFound(new { message = "Article not found." }),
        EditorialOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to perform this action on this article." }),
        EditorialOutcome.InvalidGeography => BadRequest(new { message = "The country/city is invalid, or the city does not belong to the country." }),
        EditorialOutcome.InvalidBody => BadRequest(new { message = "The article body is empty after cleaning, or too long." }),
        EditorialOutcome.InvalidFeaturedVideo => BadRequest(new { message = "Choose a ready video asset embedded in the article before adding it to the video hub." }),
        EditorialOutcome.InvalidEditReason => BadRequest(new { message = "Editing a published article requires a reason of at least 5 characters." }),
        EditorialOutcome.InvalidScheduleTime => BadRequest(new { message = "Scheduled publication must be in the future." }),
        EditorialOutcome.Conflict => Conflict(new { message = "The article is not in a state that allows this action.", article = result.Article }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };
}
