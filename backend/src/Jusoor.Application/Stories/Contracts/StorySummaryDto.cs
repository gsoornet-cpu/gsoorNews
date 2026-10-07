namespace Jusoor.Application.Stories.Contracts;

/// <summary>
/// One card's worth of data for the homepage feed. Deliberately has no
/// ImageUrl or Category — neither exists on Story/Article yet (Phase 1's
/// RSS ingestion doesn't extract images, and there's no category taxonomy
/// in the Domain model), so this DTO only exposes what's real. See Slice 8's
/// dashboard entry for what the frontend does about the gap instead of
/// pretending the data exists.
/// </summary>
public sealed record StorySummaryDto(
    Guid Id,
    string Title,
    string? Excerpt,
    DateTimeOffset? PublishedAtUtc,
    string? CountryNameAr,
    string? CountryNameEn,
    string? CityNameAr,
    string? CityNameEn,
    IReadOnlyList<string> SourceNames);
