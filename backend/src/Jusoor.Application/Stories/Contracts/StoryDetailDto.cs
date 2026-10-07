namespace Jusoor.Application.Stories.Contracts;

public sealed record StorySourceDto(string SourceName, string CanonicalUrl, DateTimeOffset? PublishedAtUtc);

/// <summary>
/// Full detail for the article page. Content comes from the single Article
/// in the Story with the longest body text — a deliberate v1 heuristic
/// (documented on the handler), not an editorial "primary source" concept
/// the Domain model has any real notion of yet. Every Article in the Story
/// is still listed under Sources, so multi-outlet coverage of the same
/// event isn't lost, just not each fully rendered.
/// </summary>
public sealed record StoryDetailDto(
    Guid Id,
    string Title,
    string? Content,
    DateTimeOffset? PublishedAtUtc,
    string? CountryNameAr,
    string? CountryNameEn,
    string? CityNameAr,
    string? CityNameEn,
    IReadOnlyList<StorySourceDto> Sources);
