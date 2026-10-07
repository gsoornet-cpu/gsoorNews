namespace Jusoor.Application.Review.Contracts;

public sealed record ReviewArticleDto(
    string SourceName,
    string Title,
    string? Content,
    string CanonicalUrl,
    DateTimeOffset? PublishedAtUtc);

public sealed record ReviewRelevanceEvaluationDto(
    bool IsRelevant,
    decimal ConfidenceScore,
    string Method,
    string Reasons,
    string EngineVersion,
    DateTimeOffset EvaluatedAtUtc);

/// <summary>
/// Full detail for the single-item review screen. Unlike the public
/// StoryDetailDto (Slice 8), this deliberately does NOT pick one "longest
/// article wins" representative body — a reviewer needs to see every
/// clustered Article in full to judge relevance responsibly, and the
/// complete relevance-evaluation history (not just the latest verdict) so
/// they can see whether this Story has been re-evaluated before and how
/// the engine's read of it has changed.
/// </summary>
public sealed record ReviewQueueItemDetailDto(
    Guid StoryId,
    string Title,
    DateTimeOffset FirstSeenAtUtc,
    DateTimeOffset LastUpdatedAtUtc,
    string? CountryNameAr,
    string? CountryNameEn,
    string? CityNameAr,
    string? CityNameEn,
    IReadOnlyList<ReviewArticleDto> Articles,
    IReadOnlyList<ReviewRelevanceEvaluationDto> RelevanceHistory);
