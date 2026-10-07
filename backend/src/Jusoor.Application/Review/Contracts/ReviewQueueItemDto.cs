namespace Jusoor.Application.Review.Contracts;

/// <summary>
/// One row in the review queue listing — enough for an editor to triage
/// without opening every item, including WHY the relevance engine couldn't
/// decide on its own (the whole reason this queue exists). Deliberately
/// mirrors Slice 8's StorySummaryDto's shape (excerpt/source/geography)
/// rather than reinventing it, plus the relevance-specific fields that only
/// matter for an internal review screen, never a public one.
/// </summary>
public sealed record ReviewQueueItemDto(
    Guid StoryId,
    string Title,
    string? Excerpt,
    DateTimeOffset FirstSeenAtUtc,
    DateTimeOffset LastUpdatedAtUtc,
    decimal? LatestConfidenceScore,
    string? LatestReasons,
    IReadOnlyList<string> SourceNames,
    string? CountryNameAr,
    string? CountryNameEn,
    string? CityNameAr,
    string? CityNameEn);
