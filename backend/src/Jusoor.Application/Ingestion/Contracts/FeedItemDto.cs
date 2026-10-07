namespace Jusoor.Application.Ingestion.Contracts;

/// <summary>
/// Normalized representation of one item from a source feed, regardless of
/// the underlying feed format. Per §8: "normalize source data into a
/// consistent internal representation before it enters the core pipeline" —
/// this record IS that internal representation.
/// </summary>
public record FeedItemDto(
    string ExternalId,
    string Title,
    string CanonicalUrl,
    string? RawContent,
    DateTimeOffset? PublishedAtUtc);
