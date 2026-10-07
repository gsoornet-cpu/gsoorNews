using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Stories.Contracts;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Stories;

/// <summary>
/// The public homepage/section feed — Slice 8. Only ever returns Stories
/// with Status == Relevant: PendingRelevanceReview and NeedsHumanReview
/// must never reach a public page (see Story.ApplyRelevanceVerdict's own
/// comment — NeedsHumanReview is "never auto-published" by design), and
/// NotRelevant obviously shouldn't either. This handler is the enforcement
/// point for that rule on the read side, same way ApplyRelevanceVerdict is
/// the enforcement point on the write side.
/// </summary>
public sealed record GetHomepageStoriesQuery(int Page = 1, int PageSize = 20) : IRequest<PagedResult<StorySummaryDto>>;

public class GetHomepageStoriesQueryValidator : AbstractValidator<GetHomepageStoriesQuery>
{
    public GetHomepageStoriesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

public class GetHomepageStoriesQueryHandler : IRequestHandler<GetHomepageStoriesQuery, PagedResult<StorySummaryDto>>
{
    private const int ExcerptMaxLength = 220;

    private readonly IApplicationDbContext _context;

    public GetHomepageStoriesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<StorySummaryDto>> Handle(GetHomepageStoriesQuery request, CancellationToken cancellationToken)
    {
        var baseQuery = _context.Stories
            .Where(s => s.Status == StoryStatus.Relevant)
            .Include(s => s.Articles)
            .AsNoTracking();

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        // Newest-first by LastUpdatedAtUtc, not FirstSeenAtUtc — a Story
        // that gains fresh coverage from a new source should resurface,
        // same "most recently active" ordering a news homepage needs.
        var stories = await baseQuery
            .OrderByDescending(s => s.LastUpdatedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var countryIds = stories.Where(s => s.PrimaryCountryId.HasValue).Select(s => s.PrimaryCountryId!.Value).Distinct().ToList();
        var cityIds = stories.Where(s => s.PrimaryCityId.HasValue).Select(s => s.PrimaryCityId!.Value).Distinct().ToList();
        // Article has no Source navigation property — it's configured as an
        // FK-only relationship (see ArticleConfiguration) — so Sources are
        // loaded the same explicit-dictionary way as Country/City below,
        // not via .Include().
        var sourceIds = stories.SelectMany(s => s.Articles).Select(a => a.SourceId).Distinct().ToList();
        var countries = await _context.Countries.Where(c => countryIds.Contains(c.Id)).AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
        var cities = await _context.Cities.Where(c => cityIds.Contains(c.Id)).AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
        var sources = await _context.Sources.Where(s => sourceIds.Contains(s.Id)).AsNoTracking().ToDictionaryAsync(s => s.Id, cancellationToken);

        var items = stories.Select(story =>
        {
            var country = story.PrimaryCountryId.HasValue && countries.TryGetValue(story.PrimaryCountryId.Value, out var c) ? c : null;
            var city = story.PrimaryCityId.HasValue && cities.TryGetValue(story.PrimaryCityId.Value, out var ci) ? ci : null;

            // Same "longest article wins as the representative copy"
            // heuristic as GetStoryByIdQueryHandler — see its comment.
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

            return new StorySummaryDto(
                story.Id,
                story.CanonicalTitle,
                excerpt,
                leadArticle?.PublishedAtUtc,
                country?.NameAr,
                country?.NameEn,
                city?.NameAr,
                city?.NameEn,
                sourceNames);
        }).ToList();

        return new PagedResult<StorySummaryDto>(items, request.Page, request.PageSize, totalCount);
    }
}
