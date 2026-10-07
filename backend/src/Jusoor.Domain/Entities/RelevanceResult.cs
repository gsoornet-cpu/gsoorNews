using Jusoor.Domain.Common;
using Jusoor.Domain.Enums;

namespace Jusoor.Domain.Entities;

/// <summary>
/// One immutable record of a single relevance-engine evaluation. Deliberately
/// append-only (no in-place score mutation) so that:
///   (a) re-running the engine after a rules/prompt change doesn't destroy
///       the previous verdict's audit trail, and
///   (b) "explainable relevance result" (§13) means something concrete —
///       you can always answer "what did the engine decide, with what
///       reasoning, using which engine version, and when" for any Story.
/// Story.Status is the CURRENT verdict; this table is the history behind it.
/// </summary>
public class RelevanceResult : BaseEntity
{
    // `required` dropped — see Article.cs's comment on the same pattern.
    public Guid StoryId { get; private set; }

    /// <summary>The specific Article whose content triggered this evaluation
    /// run — kept for audit even though relevance is scored at the Story
    /// level once articles are clustered.</summary>
    public Guid? TriggeringArticleId { get; private set; }

    public bool IsRelevant { get; private set; }

    /// <summary>0.00–1.00. Below the configured review threshold, the Story
    /// is routed to NeedsHumanReview regardless of IsRelevant — see
    /// Story.ApplyRelevanceVerdict.</summary>
    public decimal ConfidenceScore { get; private set; }

    public RelevanceMethod Method { get; private set; }

    /// <summary>Human/audit-readable signals behind the verdict, e.g.
    /// ["mentions Egyptian consulate", "diaspora-community source layer"].
    /// Stored as a newline-delimited string rather than a JSON column for
    /// this slice — genuinely structured signal taxonomy is scope for the
    /// relevance-engine-v1 slice itself, not the persistence foundation.</summary>
    public string Reasons { get; private set; } = null!;

    /// <summary>e.g. "rules-v1", "rules-v1+embeddings-v1" — lets the engine
    /// evolve without losing the ability to explain past verdicts against
    /// the rules that actually produced them.</summary>
    public string EngineVersion { get; private set; } = null!;

    public DateTimeOffset EvaluatedAtUtc { get; private set; }

    private RelevanceResult() { }

    public static RelevanceResult Create(
        Guid storyId,
        Guid? triggeringArticleId,
        bool isRelevant,
        decimal confidenceScore,
        RelevanceMethod method,
        string reasons,
        string engineVersion,
        DateTimeOffset nowUtc)
    {
        if (confidenceScore is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(confidenceScore), "Confidence score must be between 0 and 1.");
        }

        if (string.IsNullOrWhiteSpace(engineVersion))
        {
            throw new ArgumentException("A RelevanceResult must record which engine version produced it.", nameof(engineVersion));
        }

        return new RelevanceResult
        {
            StoryId = storyId,
            TriggeringArticleId = triggeringArticleId,
            IsRelevant = isRelevant,
            ConfidenceScore = confidenceScore,
            Method = method,
            Reasons = reasons,
            EngineVersion = engineVersion,
            EvaluatedAtUtc = nowUtc
        };
    }
}
