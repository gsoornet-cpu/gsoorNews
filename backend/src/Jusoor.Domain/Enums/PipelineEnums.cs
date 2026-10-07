namespace Jusoor.Domain.Enums;

/// <summary>
/// A Story is the canonical real-world event; multiple Articles (from
/// different sources/layers) can report on the same Story once clustered.
/// Status reflects the STORY's overall relevance verdict, not any single
/// article's processing state — see ArticleProcessingStatus for that.
/// </summary>
public enum StoryStatus
{
    PendingRelevanceReview = 1,
    Relevant = 2,
    NotRelevant = 3,
    NeedsHumanReview = 4 // relevance engine confidence below threshold — never auto-published
}

/// <summary>
/// Pipeline stage an individual Article has reached. Deliberately linear and
/// explicit — each stage is independently observable/testable per §17/§20,
/// and "Failed" carries a reason rather than silently disappearing.
/// </summary>
public enum ArticleProcessingStatus
{
    Fetched = 1,
    Validated = 2,
    Normalized = 3,
    Sanitized = 4,
    DuplicateCheck = 5,
    RelevanceEvaluated = 6,
    Classified = 7,
    Published = 8,
    Rejected = 9,
    Failed = 10
}

/// <summary>
/// Which relevance method actually produced a given RelevanceResult — the
/// spec requires the relevance verdict to be explainable and versionable,
/// and this is part of that explanation, not just an implementation detail.
/// </summary>
public enum RelevanceMethod
{
    RulesOnly = 1,
    Embeddings = 2,
    Llm = 3
}
