using Jusoor.Application.Common.Security;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Editorial;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

[ApiController]
[Route("api/v1/cms/articles/{id:guid}/seo")]
[Authorize(Policy = AuthorizationPolicies.CanEditEditorialSeo)]
public sealed class EditorialSeoController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IApplicationDbContext _context;
    public EditorialSeoController(ISender sender, IApplicationDbContext context) { _sender = sender; _context = context; }

    [HttpGet]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var article = await _context.EditorialArticles.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.Id, a.Title, a.Slug, a.SeoTitle, a.SeoDescription, a.CanonicalUrl,
                a.SocialTitle, a.SocialDescription, a.SocialImageUrl, a.NoIndex, a.NoFollow, a.Status,
                a.PrimaryCategoryId, a.SecondaryTags, a.TwitterTitle, a.TwitterDescription, a.TwitterImageUrl })
            .FirstOrDefaultAsync(cancellationToken);
        if (article is null) return NotFound(new { message = "Article not found." });
        var categories = await _context.EditorialCategories.AsNoTracking().Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.NameAr)
            .Select(c => new { c.Id, c.NameAr, c.Slug, c.DisplayOrder }).ToListAsync(cancellationToken);
        return Ok(new { article.Id, article.Title, article.Slug, article.SeoTitle, article.SeoDescription,
            article.CanonicalUrl, article.SocialTitle, article.SocialDescription, article.SocialImageUrl,
            article.NoIndex, article.NoFollow, article.Status, article.PrimaryCategoryId, article.SecondaryTags,
            article.TwitterTitle, article.TwitterDescription, article.TwitterImageUrl, categories });
    }

    public sealed record UpdateSeoRequest(string? SeoTitle, string? SeoDescription, string Slug, string? CanonicalUrl,
        string? SocialTitle, string? SocialDescription, string? SocialImageUrl, bool NoIndex, bool NoFollow,
        Guid? PrimaryCategoryId, string[] SecondaryTags, string? TwitterTitle, string? TwitterDescription, string? TwitterImageUrl);

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateSeoRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateEditorialSeoCommand(id, request.SeoTitle, request.SeoDescription,
            request.Slug, request.CanonicalUrl, request.SocialTitle, request.SocialDescription,
            request.SocialImageUrl, request.NoIndex, request.NoFollow, request.PrimaryCategoryId,
            request.SecondaryTags ?? Array.Empty<string>(), request.TwitterTitle, request.TwitterDescription,
            request.TwitterImageUrl), cancellationToken);
        return result.Outcome switch
        {
            EditorialOutcome.Success => Ok(result.Article),
            EditorialOutcome.NotFound => NotFound(new { message = "Article not found." }),
            EditorialOutcome.Conflict => Conflict(new { message = "This slug is already used by another article." }),
            _ => BadRequest(new { message = "SEO metadata is invalid." })
        };
    }
}
