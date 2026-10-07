namespace Jusoor.Application.Relevance;

public class RelevanceOptions
{
    public const string SectionName = "Relevance";

    /// <summary>Below this confidence, Story.ApplyRelevanceVerdict routes to
    /// NeedsHumanReview regardless of the engine's IsRelevant guess — this
    /// is deliberately NOT the same as "spec §07's 0.4–0.7 LLM-escalation
    /// band" (that band assumes an LLM step exists to resolve it, which v1
    /// does not have yet). Until an LLM/embeddings engine exists, ambiguous
    /// cases are simply routed to a human instead — the same
    /// NeedsHumanReview state Slice 1 already built for exactly this
    /// purpose, not a new mechanism invented for this slice.</summary>
    public decimal ReviewThreshold { get; set; } = 0.6m;
}