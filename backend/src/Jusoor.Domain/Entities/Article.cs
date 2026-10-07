using Jusoor.Domain.Common;
using Jusoor.Domain.Enums;

namespace Jusoor.Domain.Entities;

/// <summary>
/// A single fetched item from a single Source. Two idempotency keys are
/// modeled deliberately (per §18):
///   - (SourceId, ExternalSourceItemId): protects against re-fetching the
///     same feed item across scheduled runs.
///   - ContentHash: catches exact duplicates that arrive with a different
///     external ID (e.g. a source republishing the same text under a new
///     item ID) — this is the "exact duplicate" half of §12; near-duplicate
///     clustering across DIFFERENT sources is a separate, later slice once
///     embeddings exist, not implemented here.
/// Both are enforced as unique indexes at the Infrastructure layer, not
/// just checked in application code — retries must be safe even under
/// concurrent ingestion runs, which only a DB-level constraint guarantees.
/// </summary>
public class Article : AuditableEntity
{
    // "required" was dropped from these — they're only ever set via the
    // private static factory below, never via public object-initializer
    // syntax, so `required` had no purpose here and (correctly) fails to
    // compile with a private setter on a public class anyway.
    public Guid SourceId { get; private set; }
    public Guid? StoryId { get; private set; }

    public string ExternalSourceItemId { get; private set; } = null!;
    public string CanonicalUrl { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string ContentHash { get; private set; } = null!;

    /// <summary>Raw body text as fetched (e.g. RSS item description/content).
    /// Added in Slice 5 specifically because the Relevance Engine is the
    /// first real reader of it — before that, only its hash was persisted
    /// (see ContentHash) and the text itself was discarded after hashing.
    /// This follows the same principle already applied to the deferred
    /// pgvector Embedding column: no field is added speculatively, only
    /// once something actually reads or writes it. Nullable because a feed
    /// item's description can legitimately be empty/absent.</summary>
    public string? Content { get; private set; }

    /// <summary>ISO 639-1, e.g. "ar" or "en" — populated by the language
    /// detection pipeline stage, not guessed at creation time.</summary>
    public string? DetectedLanguage { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public DateTimeOffset FetchedAtUtc { get; private set; }

    public ArticleProcessingStatus Status { get; private set; } = ArticleProcessingStatus.Fetched;
    public string? FailureReason { get; private set; }

    private Article() { }

    public static Article Create(
        Guid sourceId,
        string externalSourceItemId,
        string canonicalUrl,
        string title,
        string contentHash,
        DateTimeOffset? publishedAtUtc,
        DateTimeOffset fetchedAtUtc,
        string? content = null)
    {
        if (string.IsNullOrWhiteSpace(externalSourceItemId))
        {
            throw new ArgumentException("An Article must carry the source's own item id for idempotency.", nameof(externalSourceItemId));
        }

        if (!Uri.TryCreate(canonicalUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            // This is a domain-level sanity check on shape, not the SSRF
            // control — SSRF protection happens at fetch time, in
            // Infrastructure, before any URL reaches this constructor. This
            // check exists so a malformed/non-http(s) URL can never even be
            // persisted, regardless of which pipeline stage produced it.
            throw new ArgumentException($"'{canonicalUrl}' is not a valid absolute http(s) URL.", nameof(canonicalUrl));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("An Article must have a non-empty title.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(contentHash))
        {
            throw new ArgumentException("An Article must carry a content hash for exact-duplicate detection.", nameof(contentHash));
        }

        return new Article
        {
            SourceId = sourceId,
            ExternalSourceItemId = externalSourceItemId,
            CanonicalUrl = canonicalUrl,
            Title = title,
            ContentHash = contentHash,
            PublishedAtUtc = publishedAtUtc,
            FetchedAtUtc = fetchedAtUtc,
            Content = content
        };
    }

    public void AdvanceTo(ArticleProcessingStatus status)
    {
        if (Status is ArticleProcessingStatus.Rejected or ArticleProcessingStatus.Failed)
        {
            throw new InvalidOperationException(
                $"Article {Id} is already terminal ({Status}) and cannot advance to {status}.");
        }

        Status = status;
    }

    public void MarkFailed(string reason)
    {
        Status = ArticleProcessingStatus.Failed;
        FailureReason = reason;
    }

    public void SetDetectedLanguage(string languageCode) => DetectedLanguage = languageCode;

    public void AssignToStory(Story story, DateTimeOffset nowUtc)
    {
        StoryId = story.Id;
        story.AttachArticle(this, nowUtc);
    }
}