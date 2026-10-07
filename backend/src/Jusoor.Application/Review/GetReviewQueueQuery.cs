using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Review.Contracts;
using Jusoor.Application.Stories.Contracts;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Review;

/// <summary>
/// The human review queue — the read side of closing Phase 1's
/// NeedsHumanReview gap (Slice 5 introduced the status; nothing since has
/// been able to act on it). Reuses Stories.Contracts.PagedResult&lt;T&gt;
/// rather than a second generic paging wrapper — same reusable-abstraction
/// reasoning as Slice 8 reusing it for the public feed.
/// </summary>
public sealed record GetReviewQueueQuery(int Page = 1, int PageSize = 20) : IRequest<PagedResult<ReviewQueueItemDto>>;

public class GetReviewQueueQueryValidator : AbstractValidator<GetReviewQueueQuery>
{
    public GetReviewQueueQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

public class GetReviewQueueQueryHandler : IRequestHandler<GetReviewQueueQuery, PagedResult<ReviewQueueItemDto>>
{
    private const int ExcerptMaxLength = 220;

    private readonly IApplicationDbContext _context;

    public GetReviewQueueQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<ReviewQueueItemDto>> Handle(GetReviewQueueQuery request, CancellationToken cancellationToken)
    {
        var baseQuery = _context.Stories
            .Where(s => s.Status == StoryStatus.NeedsHumanReview)
            .Include(s => s.Articles)
            .AsNoTracking();

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        // Oldest-first (FIFO), not newest-first like the public feed: a
        // review queue that always surfaces the newest arrivals lets old
        // items starve at the bottom forever. Fairness to whichever Story
        // has been waiting longest is the whole point of a queue.
        var stories = await baseQuery
            .OrderBy(s => s.FirstSeenAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var storyIds = stories.Select(s => s.Id).ToList();
        var countryIds = stories.Where(s => s.PrimaryCountryId.HasValue).Select(s => s.PrimaryCountryId!.Value).Distinct().ToList();
        var cityIds = stories.Where(s => s.PrimaryCityId.HasValue).Select(s => s.PrimaryCityId!.Value).Distinct().ToList();
        var sourceIds = stories.SelectMany(s => s.Articles).Select(a => a.SourceId).Distinct().ToList();

        var countries = await _context.Countries.Where(c => countryIds.Contains(c.Id)).AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
        var cities = await _context.Cities.Where(c => cityIds.Contains(c.Id)).AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
        var sources = await _context.Sources.Where(s => sourceIds.Contains(s.Id)).AsNoTracking().ToDictionaryAsync(s => s.Id, cancellationToken);

        // The latest RelevanceResult per Story is the "why is this here"
        // context the queue exists to show — one extra round trip, kept
        // separate from the main paginated query rather than an OUTER APPLY
        // to keep this readable and match the rest of the codebase's
        // "load, then look up in memory" style (see Slice 8/6's handlers).
        var latestEvaluations = await _context.RelevanceResults
            .Where(r => storyIds.Contains(r.StoryId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var latestByStory = latestEvaluations
            .GroupBy(r => r.StoryId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.EvaluatedAtUtc).First());

        var items = stories.Select(story =>
        {
            var country = story.PrimaryCountryId.HasValue && countries.TryGetValue(story.PrimaryCountryId.Value, out var c) ? c : null;
            var city = story.PrimaryCityId.HasValue && cities.TryGetValue(story.PrimaryCityId.Value, out var ci) ? ci : null;
            var latest = latestByStory.GetValueOrDefault(story.Id);

            var leadArticle = story.Articles.OrderByDescending(a => a.Content?.Length ?? 0).FirstOrDefault();
            var excerpt = leadArticle?.Content is { Length: > 0 } content
                ? (content.Length > ExcerptMaxLength ? content[..ExcerptMaxLength].TrimEnd() + "…" : content)
                : null;

            var sourceNames = story.Articles
                .Select(a => sources.TryGetValue(a.SourceId, out var src) ? src.Name : null)
                .Where(name => name is not null)
                .Select(name => name!)
                .Distinct()
                .ToList();

            return new ReviewQueueItemDto(
                story.Id,
                story.CanonicalTitle,
                excerpt,
                story.FirstSeenAtUtc,
                story.LastUpdatedAtUtc,
                latest?.ConfidenceScore,
                latest?.Reasons,
                sourceNames,
                country?.NameAr,
                country?.NameEn,
                city?.NameAr,
                city?.NameEn);
        }).ToList();

        return new PagedResult<ReviewQueueItemDto>(items, request.Page, request.PageSize, totalCount);
    }
}
