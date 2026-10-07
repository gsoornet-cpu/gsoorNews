using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Stories.Contracts;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Stories;

/// <summary>
/// The article page — Slice 8. Returns null (→ 404 at the controller) for
/// anything that isn't Status == Relevant, same enforcement point as
/// GetHomepageStoriesQuery and for the same reason: no route into this
/// query may leak a NeedsHumanReview/Pending/NotRelevant Story to the
/// public, even by guessing its id.
/// </summary>
public sealed record GetStoryByIdQuery(Guid StoryId) : IRequest<StoryDetailDto?>;

public class GetStoryByIdQueryValidator : AbstractValidator<GetStoryByIdQuery>
{
    public GetStoryByIdQueryValidator()
    {
        RuleFor(x => x.StoryId).NotEmpty();
    }
}

public class GetStoryByIdQueryHandler : IRequestHandler<GetStoryByIdQuery, StoryDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetStoryByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StoryDetailDto?> Handle(GetStoryByIdQuery request, CancellationToken cancellationToken)
    {
        var story = await _context.Stories
            .Where(s => s.Id == request.StoryId && s.Status == StoryStatus.Relevant)
            .Include(s => s.Articles)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (story is null)
        {
            return null;
        }

        var country = story.PrimaryCountryId.HasValue
            ? await _context.Countries.AsNoTracking().FirstOrDefaultAsync(c => c.Id == story.PrimaryCountryId.Value, cancellationToken)
            : null;
        var city = story.PrimaryCityId.HasValue
            ? await _context.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Id == story.PrimaryCityId.Value, cancellationToken)
            : null;

        var sourceIds = story.Articles.Select(a => a.SourceId).Distinct().ToList();
        var sources = await _context.Sources.Where(s => sourceIds.Contains(s.Id)).AsNoTracking().ToDictionaryAsync(s => s.Id, cancellationToken);

        // v1 heuristic: the Article with the longest body text stands in as
        // the page's rendered content. The Domain model has no "primary
        // source" concept for a Story yet (all Articles in a cluster are
        // peers — see StoryClusteringService), so this is a deliberately
        // simple, deterministic pick rather than an invented editorial
        // ranking. Every Article is still listed under Sources below, so
        // no coverage is silently dropped, just not each fully rendered.
        var leadArticle = story.Articles.OrderByDescending(a => a.Content?.Length ?? 0).FirstOrDefault();

        var sourceDtos = story.Articles
            .OrderByDescending(a => a.PublishedAtUtc)
            .Select(a => new StorySourceDto(
                sources.TryGetValue(a.SourceId, out var src) ? src.Name : "Unknown source",
                a.CanonicalUrl,
                a.PublishedAtUtc))
            .ToList();

        return new StoryDetailDto(
            story.Id,
            story.CanonicalTitle,
            leadArticle?.Content,
            leadArticle?.PublishedAtUtc,
            country?.NameAr,
            country?.NameEn,
            city?.NameAr,
            city?.NameEn,
            sourceDtos);
    }
}
