using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Relevance.Contracts;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;

namespace Jusoor.Application.Relevance;

/// <summary>
/// Relevance Engine v1 — the deterministic half of spec §07's hybrid
/// classifier ("a cheap, deterministic model — Embeddings + Keyword/Entity
/// rules — filters first, then a stronger LLM is called only for edge
/// cases"). This class is ONLY the Keyword/Entity rules half. The
/// Embeddings half and the LLM-escalation half are deliberately NOT
/// implemented here — both need infrastructure (pgvector reads, an AI
/// Gateway/Semantic Kernel call) that doesn't exist yet, and per this
/// project's own established principle (see the deferred pgvector Embedding
/// column, and Article.Content's own comment), that infrastructure isn't
/// worth adding before something real uses it.
///
/// Instead of faking the LLM-escalation step, ambiguous matches are given a
/// deliberately LOW confidence score (below RelevanceOptions.ReviewThreshold),
/// which routes the Story to Story.NeedsHumanReview — a real mechanism this
/// project already has (Slice 1), not a placeholder invented for this slice.
/// A confident "not relevant" verdict gets a HIGH confidence score (it is
/// certain, just certain in the negative direction) so it does NOT get
/// routed to review — confidence measures certainty of the verdict, not
/// "probability of being relevant"; see RelevanceResult's own doc comment.
/// </summary>
public class RulesBasedRelevanceEngine : IRelevanceEngine
{
    public const string EngineVersion = "rules-v1";

    // Direct, unambiguous "this is about Egyptians abroad" signals — spec
    // §07's own examples: an Egyptian who died/was arrested abroad, an
    // embassy/consulate announcement, a migration law affecting Egyptians
    // directly. Phrases, not single words, specifically so a bare country
    // or nationality mention (handled separately, below) doesn't
    // accidentally count as a strong signal.
    private static readonly string[] StrongSignals =
    [
        "المصريين بالخارج", "الجالية المصرية", "مصريين بالخارج", "أبناء الجالية المصرية",
        "سفارة مصر", "السفارة المصرية", "قنصلية مصر", "القنصلية المصرية",
        "وزارة الخارجية المصرية", "الخارجية المصرية",
        "egyptian embassy", "egyptian consulate", "egyptian community",
        "egyptians abroad", "egyptian expatriate", "egyptian expatriates", "egyptian diaspora"
    ];

    // A bare nationality reference — real signal (spec's own "Egyptian
    // died/arrested abroad" example needs nothing more than this), but
    // weaker/less specific than a StrongSignals phrase, so it scores lower.
    private static readonly string[] NationalitySignals =
    [
        "مصري", "مصرية", "مصريين", "المصري", "المصرية", "المصريين",
        "egyptian", "egyptians"
    ];

    // A bare place-name mention with NO nationality/diaspora signal
    // alongside it — e.g. an international story that merely happens to be
    // set in Cairo. Genuinely ambiguous per spec §07's own "international
    // event in the same city with no direct Egyptian link" example.
    private static readonly string[] AmbiguousPlaceSignals =
    [
        "مصر", "القاهرة", "الإسكندرية", "egypt", "cairo", "alexandria"
    ];

    private const decimal ConfidentScore = 0.90m;
    private const decimal NationalityOnlyScore = 0.75m;
    private const decimal AmbiguousScore = 0.50m;

    public Task<RelevanceEvaluation> EvaluateAsync(Story story, CancellationToken cancellationToken)
    {
        var corpus = BuildCorpus(story);

        var matchedStrong = StrongSignals.Where(s => corpus.Contains(s, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matchedStrong.Length > 0)
        {
            return Task.FromResult(new RelevanceEvaluation(
                IsRelevant: true,
                ConfidenceScore: ConfidentScore,
                Method: RelevanceMethod.RulesOnly,
                Reasons: $"Matched diaspora-specific signal(s): {string.Join(", ", matchedStrong)}",
                EngineVersion: EngineVersion));
        }

        var matchedNationality = NationalitySignals.Where(s => corpus.Contains(s, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matchedNationality.Length > 0)
        {
            return Task.FromResult(new RelevanceEvaluation(
                IsRelevant: true,
                ConfidenceScore: NationalityOnlyScore,
                Method: RelevanceMethod.RulesOnly,
                Reasons: $"Matched Egyptian-nationality signal(s): {string.Join(", ", matchedNationality)}",
                EngineVersion: EngineVersion));
        }

        var matchedAmbiguous = AmbiguousPlaceSignals.Where(s => corpus.Contains(s, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matchedAmbiguous.Length > 0)
        {
            return Task.FromResult(new RelevanceEvaluation(
                IsRelevant: true, // best-effort guess only — confidence below threshold routes this to human review regardless
                ConfidenceScore: AmbiguousScore,
                Method: RelevanceMethod.RulesOnly,
                Reasons: $"Only a bare place mention found, no clear diaspora/nationality signal: {string.Join(", ", matchedAmbiguous)}. Needs stronger evaluation than rules-v1 can provide.",
                EngineVersion: EngineVersion));
        }

        return Task.FromResult(new RelevanceEvaluation(
            IsRelevant: false,
            ConfidenceScore: ConfidentScore,
            Method: RelevanceMethod.RulesOnly,
            Reasons: "No Egyptian-diaspora, nationality, or place signal found.",
            EngineVersion: EngineVersion));
    }

    private static string BuildCorpus(Story story)
    {
        // Every clustered Article's Title + Content — a Story can carry
        // multiple sources' wording of the same event, and a signal present
        // in any one of them is a real signal for the Story as a whole.
        var parts = story.Articles.SelectMany(a => new[] { a.Title, a.Content ?? string.Empty });
        return string.Join(" \n ", parts);
    }
}