using Jusoor.Domain.Common;
using Jusoor.Domain.Enums;

namespace Jusoor.Domain.Entities;

/// <summary>
/// The canonical real-world event. Created the moment an Article can't be
/// clustered onto an existing Story (exact/near-duplicate detection, see
/// Article.ContentHash for the exact-match half of that — near-duplicate
/// clustering via embeddings is deliberately NOT built here, see the
/// migration notes for why).
/// </summary>
public class Story : AuditableEntity
{
    // See Article.cs's comment on why `required` was dropped here too —
    // only ever constructed via the private factory below.
    public string CanonicalTitle { get; private set; } = null!;
    public StoryStatus Status { get; private set; } = StoryStatus.PendingRelevanceReview;

    public Guid? PrimaryCountryId { get; private set; }
    public Guid? PrimaryCityId { get; private set; }

    public DateTimeOffset FirstSeenAtUtc { get; private set; }
    public DateTimeOffset LastUpdatedAtUtc { get; private set; }

    private readonly List<Article> _articles = new();
    public IReadOnlyCollection<Article> Articles => _articles.AsReadOnly();

    // EF Core requires a parameterless constructor for materialization;
    // private so application code can't construct an invalid Story by
    // skipping the factory below.
    private Story() { }

    public static Story Create(string canonicalTitle, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(canonicalTitle))
        {
            throw new ArgumentException("A Story must have a non-empty canonical title.", nameof(canonicalTitle));
        }

        return new Story
        {
            CanonicalTitle = canonicalTitle,
            FirstSeenAtUtc = nowUtc,
            LastUpdatedAtUtc = nowUtc
        };
    }

    public void AttachArticle(Article article, DateTimeOffset nowUtc)
    {
        _articles.Add(article);
        LastUpdatedAtUtc = nowUtc;
    }

    public void SetGeography(Guid? countryId, Guid? cityId)
    {
        PrimaryCountryId = countryId;
        PrimaryCityId = cityId;
    }

    /// <summary>
    /// Only a RelevanceResult drives this transition — nothing else in the
    /// system may set Status directly, which is the whole point of the
    /// engine being "explainable": every status change traces back to a
    /// specific, recorded verdict.
    /// </summary>
    public void ApplyRelevanceVerdict(bool isRelevant, decimal confidenceScore, decimal reviewThreshold, DateTimeOffset nowUtc)
    {
        Status = confidenceScore < reviewThreshold
            ? StoryStatus.NeedsHumanReview
            : isRelevant ? StoryStatus.Relevant : StoryStatus.NotRelevant;

        LastUpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// The human-review counterpart to ApplyRelevanceVerdict — same "every
    /// status change traces back to a specific, recorded verdict" principle
    /// applies here too, except the verdict is a named editor's judgment
    /// call (recorded separately via HumanReviewDecision, one row per
    /// decision) rather than the relevance engine's automated score.
    ///
    /// Only callable on a Story actually awaiting review. The Application
    /// layer checks this too before calling in (so it can return a proper
    /// 409 Conflict instead of an unhandled exception), but the invariant
    /// belongs here regardless — a domain entity must not trust its
    /// callers to have checked first, the same reasoning as
    /// RelevanceResult.Create validating ConfidenceScore's range even
    /// though its only caller already validates it too.
    /// </summary>
    public void ResolveHumanReview(bool isRelevant, DateTimeOffset nowUtc)
    {
        if (Status != StoryStatus.NeedsHumanReview)
        {
            throw new InvalidOperationException(
                $"Story {Id} cannot be resolved by human review because its status is {Status}, not {StoryStatus.NeedsHumanReview}.");
        }

        Status = isRelevant ? StoryStatus.Relevant : StoryStatus.NotRelevant;
        LastUpdatedAtUtc = nowUtc;
    }
}
