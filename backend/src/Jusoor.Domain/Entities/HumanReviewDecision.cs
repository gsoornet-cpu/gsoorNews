using Jusoor.Domain.Common;

namespace Jusoor.Domain.Entities;

/// <summary>
/// One immutable record of a single human review decision — the editorial
/// counterpart to RelevanceResult, which records the relevance engine's
/// automated verdicts. Deliberately a separate entity rather than a fourth
/// RelevanceMethod value on RelevanceResult: a human review decision is a
/// named, accountable person's judgment call with mandatory reasoning, not
/// a versioned algorithm's score, and RelevanceResult's Method/EngineVersion
/// fields are specifically about explaining automated verdicts across
/// engine changes — a different shape of "why" than an editor's reasoning.
///
/// Append-only by the same reasoning as RelevanceResult: Story.Status is
/// the CURRENT verdict, this table is the accountable history behind any
/// verdict that a human — not the engine — actually decided. Nothing ever
/// updates a row here; a re-review (if ever needed) is a new row, not a
/// mutation, so "who decided what, when, and why" can never be quietly
/// rewritten.
/// </summary>
public class HumanReviewDecision : BaseEntity
{
    public Guid StoryId { get; private set; }

    /// <summary>The reviewing editor's Identity user id (string — matches
    /// AuditableEntity.CreatedByUserId's type, since ASP.NET Identity's
    /// default user id is a string, not a Guid). Always populated: unlike
    /// RelevanceResult, there is no automated path that creates this
    /// entity, so there is no case where "who decided this" is unknown.</summary>
    public string ReviewedByUserId { get; private set; } = null!;

    public bool IsRelevant { get; private set; }

    /// <summary>Mandatory — a human overriding/resolving the relevance
    /// engine's uncertainty is exactly the kind of editorial judgment call
    /// spec §24 requires be explainable, not a silent click. Enforced here
    /// (the invariant) and again by ResolveReviewCommandValidator (the
    /// user-facing message) — same defense-in-depth reasoning as every
    /// other validated field on this and sibling entities.</summary>
    public string Reasoning { get; private set; } = null!;

    public DateTimeOffset DecidedAtUtc { get; private set; }

    private HumanReviewDecision() { }

    public static HumanReviewDecision Create(
        Guid storyId,
        string reviewedByUserId,
        bool isRelevant,
        string reasoning,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(reviewedByUserId))
        {
            throw new ArgumentException("A HumanReviewDecision must record who made it.", nameof(reviewedByUserId));
        }

        if (string.IsNullOrWhiteSpace(reasoning))
        {
            throw new ArgumentException("A HumanReviewDecision must record why it was made.", nameof(reasoning));
        }

        return new HumanReviewDecision
        {
            StoryId = storyId,
            ReviewedByUserId = reviewedByUserId,
            IsRelevant = isRelevant,
            Reasoning = reasoning,
            DecidedAtUtc = nowUtc
        };
    }
}
