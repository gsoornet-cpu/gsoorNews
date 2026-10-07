using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Review.Contracts;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Review;

/// <summary>
/// Full detail for one review-queue item. Returns null (→ 404 at the
/// controller) for anything not currently NeedsHumanReview — same "the
/// query is itself an access-control boundary" pattern Slice 8 established
/// for the public endpoints, applied here to protect against reviewing
/// (or leaking the moderation-relevant detail of) a Story that isn't
/// actually awaiting review.
/// </summary>
public sealed record GetReviewQueueItemQuery(Guid StoryId) : IRequest<ReviewQueueItemDetailDto?>;

public class GetReviewQueueItemQueryValidator : AbstractValidator<GetReviewQueueItemQuery>
{
    public GetReviewQueueItemQueryValidator()
    {
        RuleFor(x => x.StoryId).NotEmpty();
    }
}

public class GetReviewQueueItemQueryHandler : IRequestHandler<GetReviewQueueItemQuery, ReviewQueueItemDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetReviewQueueItemQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReviewQueueItemDetailDto?> Handle(GetReviewQueueItemQuery request, CancellationToken cancellationToken)
    {
        var story = await _context.Stories
            .Where(s => s.Id == request.StoryId && s.Status == StoryStatus.NeedsHumanReview)
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

        var articles = story.Articles
            .OrderByDescending(a => a.PublishedAtUtc)
            .Select(a => new ReviewArticleDto(
                sources.TryGetValue(a.SourceId, out var src) ? src.Name : "Unknown source",
                a.Title,
                a.Content,
                a.CanonicalUrl,
                a.PublishedAtUtc))
            .ToList();

        var relevanceHistory = await _context.RelevanceResults
            .Where(r => r.StoryId == story.Id)
            .OrderByDescending(r => r.EvaluatedAtUtc)
            .AsNoTracking()
            .Select(r => new ReviewRelevanceEvaluationDto(
                r.IsRelevant, r.ConfidenceScore, r.Method.ToString(), r.Reasons, r.EngineVersion, r.EvaluatedAtUtc))
            .ToListAsync(cancellationToken);

        return new ReviewQueueItemDetailDto(
            story.Id,
            story.CanonicalTitle,
            story.FirstSeenAtUtc,
            story.LastUpdatedAtUtc,
            country?.NameAr,
            country?.NameEn,
            city?.NameAr,
            city?.NameEn,
            articles,
            relevanceHistory);
    }
}
