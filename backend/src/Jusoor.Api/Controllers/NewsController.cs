using Jusoor.Application.Editorial;
using Jusoor.Application.Editorial.Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

/// <summary>
/// Public, anonymous read API for newsroom-published articles. The feed serves
/// ONLY EditorialArticles with Status == Published; the detail endpoint also
/// serves Archived articles (flagged) and answers 410 Gone with the public
/// retraction notice — never the text — for Retracted ones. Drafts and every
/// other non-public state are indistinguishable from non-existent ids (404).
/// All of this is enforced in the query handlers, not here.
/// </summary>
[ApiController]
[Route("api/v1/news")]
public class NewsController : ControllerBase
{
    private readonly ISender _mediator;

    public NewsController(ISender mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublished([FromQuery] int page, [FromQuery] int pageSize, [FromQuery] string? desk, [FromQuery] string? q, CancellationToken cancellationToken)
    {
        var query = new GetPublishedNewsQuery(page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize, desk, q);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("videos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublishedVideos(
        [FromQuery] int page, [FromQuery] int pageSize, [FromQuery] bool home, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetPublishedVideosQuery(page == 0 ? 1 : page, pageSize == 0 ? 18 : pageSize, home), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lean, anonymous feed for the website's sitemap.xml / news-sitemap.xml
    /// (Slice 18, spec §16). Published and Archived articles (each row carries
    /// an isArchived flag; Retracted never), newest first, no bodies.
    /// The literal "sitemap" segment cannot collide with the {id:guid} route.
    /// </summary>
    [HttpGet("sitemap")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSitemapEntries(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPublishedNewsSitemapQuery(), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// 200 — the article (Published, or Archived with <c>isArchived: true</c>).
    /// 410 — the article was retracted: the body is the public notice only
    /// (<see cref="PublicRetractionNoticeDto"/>), with <c>Cache-Control: no-store</c>
    /// so a CDN or browser never pins the retraction state (or its later reversal).
    /// 404 — unknown id, or any state that is not public.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PublicNewsDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PublicRetractionNoticeDto), StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublishedById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPublishedNewsByIdQuery(id), cancellationToken);

        switch (result.Outcome)
        {
            case PublicNewsDetailOutcome.Found:
                return Ok(result.Article);

            case PublicNewsDetailOutcome.Retracted:
                Response.Headers.CacheControl = "no-store";
                return StatusCode(StatusCodes.Status410Gone, result.Retraction);

            default:
                return NotFound(new { message = "Article not found." });
        }
    }
}
