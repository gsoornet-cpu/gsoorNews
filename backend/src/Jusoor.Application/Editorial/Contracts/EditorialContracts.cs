namespace Jusoor.Application.Editorial.Contracts;

public sealed record EditorialMediaDto(
    Guid Id,
    string FileName,
    string Kind,
    string ContentType,
    long SizeBytes,
    string AltText,
    string Credit,
    string? Caption,
    int? FocalPointX,
    int? FocalPointY,
    string PublicUrl,
    DateTimeOffset ReadyAtUtc);

/// <summary>
/// Full CMS view of an article. Never returned by a public endpoint.
/// <c>Body</c> is always HTML (Slice 21): sanitized HTML as stored, or — for an article
/// that pre-dates the rich-text slice — its plain text converted to encoded paragraphs.
/// </summary>
public sealed record EditorialArticleDto(
    Guid Id,
    string Title,
    string? Summary,
    string Body,
    string Status,
    Guid? CountryId,
    Guid? CityId,
    string OwnerUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastModifiedAtUtc,
    DateTimeOffset? PublishedAtUtc)
{
    public DateTimeOffset? ScheduledPublishAtUtc { get; init; }
    public string? AuthorName { get; init; }
    public string Slug { get; init; } = "";
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public string? CanonicalUrl { get; init; }
    public string? SocialTitle { get; init; }
    public string? SocialDescription { get; init; }
    public string? SocialImageUrl { get; init; }
    public bool NoIndex { get; init; }
    public bool NoFollow { get; init; }
    public Guid? PrimaryCategoryId { get; init; }
    public string[] SecondaryTags { get; init; } = Array.Empty<string>();
    public string[] PresentationDesks { get; init; } = Array.Empty<string>();
    public Guid? FeaturedVideoMediaAssetId { get; init; }
    public string? TwitterTitle { get; init; }
    public string? TwitterDescription { get; init; }
    public string? TwitterImageUrl { get; init; }
}

/// <summary>CMS list row (no body).</summary>
public sealed record EditorialArticleSummaryDto(
    Guid Id,
    string Title,
    string? Summary,
    string Status,
    string OwnerUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastModifiedAtUtc,
    DateTimeOffset? PublishedAtUtc)
{
    public DateTimeOffset? ScheduledPublishAtUtc { get; init; }
}

/// <summary>Public list row. Contains no internal user ids and no draft data.</summary>
public sealed record PublicNewsSummaryDto(
    Guid Id,
    string Title,
    string? Excerpt,
    DateTimeOffset PublishedAtUtc,
    string? CountryNameAr,
    string? CountryNameEn,
    string? CityNameAr,
    string? CityNameEn)
{
    public string Slug { get; init; } = "";
    public string[] PresentationDesks { get; init; } = Array.Empty<string>();
    /// <summary>Public editorial cover image, sourced from the article's social image setting.</summary>
    public string? ImageUrl { get; init; }
    public string? AuthorName { get; init; }
}

/// <summary>Published article with a CMS-selected, verified video media asset for the public video hub.</summary>
public sealed record PublicVideoSummaryDto(
    Guid Id, string Title, string? Excerpt, string VideoUrl, string? Caption, string Credit,
    DateTimeOffset PublishedAtUtc, string Slug)
{
    public string? ThumbnailUrl { get; init; }
    public string? AuthorName { get; init; }
}

/// <summary>
/// Public article view. <see cref="UpdatedAtUtc"/> is the later of the
/// publication time and the last edit (spec §16 lists "تاريخ آخر تحديث" as a
/// trust signal; it also feeds Schema.org dateModified). It is never earlier
/// than <see cref="PublishedAtUtc"/>. <c>Body</c> is sanitized HTML (Slice 21, decision D2).
/// </summary>
public sealed record PublicNewsDetailDto(
    Guid Id,
    string Title,
    string? Summary,
    string Body,
    DateTimeOffset PublishedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? CountryNameAr,
    string? CountryNameEn,
    string? CityNameAr,
    string? CityNameEn,
    IReadOnlyList<PublicCorrectionDto> Corrections,
    bool IsArchived,
    DateTimeOffset? ArchivedAtUtc)
{
    public string Slug { get; init; } = "";
    public string? AuthorName { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public string? CanonicalUrl { get; init; }
    public string? SocialTitle { get; init; }
    public string? SocialDescription { get; init; }
    public string? SocialImageUrl { get; init; }
    public bool NoIndex { get; init; }
    public bool NoFollow { get; init; }
    public string? PrimaryCategoryNameAr { get; init; }
    public string[] SecondaryTags { get; init; } = Array.Empty<string>();
    public string[] PresentationDesks { get; init; } = Array.Empty<string>();
    public string? TwitterTitle { get; init; }
    public string? TwitterDescription { get; init; }
    public string? TwitterImageUrl { get; init; }
}

/// <summary>
/// What a reader gets for a RETRACTED article (Slice 20b, decision Q9): the
/// headline, when it was first published, when it was retracted and the public
/// notice — and nothing else. The shape is the guarantee: there is no body, summary,
/// geography, corrections or user id here to leak, so a retracted text cannot
/// reach a reader through this type even if a query is later edited carelessly.
/// A test pins the exact property list.
/// </summary>
public sealed record PublicRetractionNoticeDto(
    Guid Id,
    string Title,
    DateTimeOffset PublishedAtUtc,
    DateTimeOffset RetractedAtUtc,
    string Notice);

public enum PublicNewsDetailOutcome
{
    /// <summary>Published or Archived: the full article is public (an archived one is flagged on the DTO).</summary>
    Found,

    /// <summary>Retracted: only <see cref="PublicRetractionNoticeDto"/> is public (HTTP 410).</summary>
    Retracted,

    /// <summary>Draft, InReview, Approved, unknown id — indistinguishable by design (HTTP 404).</summary>
    NotFound
}

/// <summary>
/// Three-way answer of the public detail query. Exactly one payload is set, and
/// only for its own outcome — the factories are the only way to build one, so
/// "Retracted with an article body" is not a state the type can express.
/// </summary>
public sealed record PublicNewsDetailResult
{
    private PublicNewsDetailResult(
        PublicNewsDetailOutcome outcome, PublicNewsDetailDto? article, PublicRetractionNoticeDto? retraction)
    {
        Outcome = outcome;
        Article = article;
        Retraction = retraction;
    }

    public PublicNewsDetailOutcome Outcome { get; }

    /// <summary>Set only when <see cref="Outcome"/> is <see cref="PublicNewsDetailOutcome.Found"/>.</summary>
    public PublicNewsDetailDto? Article { get; }

    /// <summary>Set only when <see cref="Outcome"/> is <see cref="PublicNewsDetailOutcome.Retracted"/>.</summary>
    public PublicRetractionNoticeDto? Retraction { get; }

    public static PublicNewsDetailResult Found(PublicNewsDetailDto article)
        => new(PublicNewsDetailOutcome.Found, article, null);

    public static PublicNewsDetailResult Retracted(PublicRetractionNoticeDto notice)
        => new(PublicNewsDetailOutcome.Retracted, null, notice);

    public static PublicNewsDetailResult NotFound()
        => new(PublicNewsDetailOutcome.NotFound, null, null);
}

/// <summary>
/// A public editorial note on a published article ("تنويه صحفي / تصحيح", decision
/// D4). Deliberately contains no issuer id or roles — those are CMS-only.
/// </summary>
public sealed record PublicCorrectionDto(string Kind, string Note, bool IsMajor, DateTimeOffset IssuedAtUtc);

/// <summary>
/// One row of the public sitemap feed: just enough for the website to build
/// sitemap.xml and the Google News sitemap. No body, no user ids, no status.
/// The public slug is included so both sitemaps emit canonical article URLs.
/// <see cref="IsArchived"/> is the one editorial flag the website needs: archived
/// articles stay in sitemap.xml (still readable, still indexable) but are NOT news
/// any more, so they must be left out of the Google News sitemap (Slice 20b).
/// </summary>
public sealed record PublicNewsSitemapEntryDto(
    Guid Id,
    string Title,
    DateTimeOffset PublishedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool IsArchived)
{
    public string Slug { get; init; } = "";
}

// ---------- CMS history (Slice 20, decision D4) ----------

/// <summary>One workflow transition (Slice 19 audit row), including the reason given when an article was returned to its author.</summary>
public sealed record EditorialTransitionDto(
    string FromStatus,
    string ToStatus,
    string ActorUserId,
    string ActorRoles,
    string? Reason,
    DateTimeOffset OccurredAtUtc);

/// <summary>A revision row WITHOUT its content — the history list stays small however long the article is.</summary>
public sealed record EditorialRevisionSummaryDto(
    int RevisionNumber,
    string Title,
    string EditedByUserId,
    string EditorRoles,
    string? Reason,
    DateTimeOffset EditedAtUtc);

/// <summary>The full content of an article as it was before revision <see cref="RevisionNumber"/>'s edit.</summary>
public sealed record EditorialRevisionDto(
    int RevisionNumber,
    string Title,
    string? Summary,
    string Body,
    Guid? CountryId,
    Guid? CityId,
    string EditedByUserId,
    string EditorRoles,
    string? Reason,
    DateTimeOffset EditedAtUtc)
{
    public string? AuthorName { get; init; }
}

public sealed record EditorialCorrectionDto(
    Guid Id,
    string Kind,
    string Note,
    bool IsMajor,
    string IssuedByUserId,
    string IssuerRoles,
    DateTimeOffset IssuedAtUtc);

/// <summary>Everything the CMS history view shows for one article, oldest first.</summary>
public sealed record EditorialHistoryDto(
    IReadOnlyList<EditorialTransitionDto> Transitions,
    IReadOnlyList<EditorialRevisionSummaryDto> Revisions,
    IReadOnlyList<EditorialCorrectionDto> Corrections);
