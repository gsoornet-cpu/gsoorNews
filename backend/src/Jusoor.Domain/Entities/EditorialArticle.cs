using Jusoor.Domain.Common;
using Jusoor.Domain.Enums;
using Jusoor.Domain.Editorial;

namespace Jusoor.Domain.Entities;

/// <summary>
/// A newsroom-authored article created through the CMS. Deliberately NOT an
/// <see cref="Article"/>: that entity is a single-source report produced by
/// the ingestion pipeline (it requires a SourceId, an external item id, a
/// canonical URL and a content hash), and reusing it would mean fabricating
/// all four. EditorialArticle is the human-written counterpart.
///
/// Nothing is publicly visible until <see cref="Publish"/> has been called;
/// every public list/feed read path must filter on Status == Published.
/// Slice 20b adds two post-publication states that are NOT in the feeds:
/// <see cref="EditorialArticleStatus.Archived"/> (still readable by URL) and
/// <see cref="EditorialArticleStatus.Retracted"/> (only a public notice is
/// shown, never the body). Which role may enter or leave them is an
/// application-layer rule (EditorialArticleAccess); the domain only guards
/// the source state, as it does for every other transition.
/// </summary>
public class EditorialArticle : AuditableEntity
{
    public const int TitleMaxLength = 300;
    public const int SummaryMaxLength = 500;
    public const int BodyMaxLength = 100_000;
    public const int RetractionNoticeMaxLength = 1000;

    public string Title { get; private set; } = null!;
    public string? Summary { get; private set; }
    /// <summary>Public byline entered by the newsroom; null uses the site's newsroom default.</summary>
    public string? AuthorName { get; private set; }

    // Slice 23 SEO metadata. Null overrides inherit the newsroom title/summary.
    public string? SeoTitle { get; private set; }
    public string? SeoDescription { get; private set; }
    public string Slug { get; private set; } = null!;
    public string? CanonicalUrl { get; private set; }
    public string? SocialTitle { get; private set; }
    public string? SocialDescription { get; private set; }
    public string? SocialImageUrl { get; private set; }
    public string? TwitterTitle { get; private set; }
    public string? TwitterDescription { get; private set; }
    public string? TwitterImageUrl { get; private set; }
    public bool NoIndex { get; private set; }
    public bool NoFollow { get; private set; }
    public Guid? PrimaryCategoryId { get; private set; }
    public EditorialCategory? PrimaryCategory { get; private set; }
    public List<string> SecondaryTags { get; private set; } = new();
    /// <summary>Final Demo presentation desks; intentionally separate from topical categories and SEO tags.</summary>
    public List<string> PresentationDesks { get; private set; } = new();
    /// <summary>The embedded video selected for the public video hub; null means article-only placement.</summary>
    public Guid? FeaturedVideoMediaAssetId { get; private set; }

    /// <summary>
    /// Editor-curated position (1-based) in the homepage video hero. Null = not on the homepage.
    /// Only meaningful while the article is Published with a ready featured video.
    /// </summary>
    public int? HomeVideoOrder { get; private set; }
    public EditorialMediaAsset? FeaturedVideoMediaAsset { get; private set; }

    /// <summary>
    /// The article text. Interpreted according to <see cref="BodyFormat"/>:
    /// sanitized HTML for everything written from Slice 21 (decision D2), plain
    /// text for older rows. The domain does NOT sanitize — it cannot (no HTML
    /// parser here, and the allow-list is a policy decision). Callers must pass
    /// HTML that has already been through the application layer's
    /// IEditorialHtmlSanitizer; the only code path that writes a body does.
    /// </summary>
    public string Body { get; private set; } = null!;

    /// <summary>
    /// Format of <see cref="Body"/>. Always <see cref="ArticleBodyFormat.Html"/> after
    /// <see cref="Create"/> or <see cref="UpdateContent"/>; <see cref="ArticleBodyFormat.PlainText"/>
    /// only for rows that pre-date the rich-text slice (back-filled by the migration).
    /// </summary>
    public ArticleBodyFormat BodyFormat { get; private set; } = ArticleBodyFormat.Html;

    public Guid? CountryId { get; private set; }
    public Guid? CityId { get; private set; }

    /// <summary>
    /// The author. Stored explicitly (not inferred from CreatedByUserId,
    /// which is set by a SaveChanges interceptor) because "own items only"
    /// authorization depends on it and must not rely on interceptor side effects.
    /// </summary>
    public string OwnerUserId { get; private set; } = null!;

    public EditorialArticleStatus Status { get; private set; } = EditorialArticleStatus.Draft;

    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public string? PublishedByUserId { get; private set; }
    public DateTimeOffset? ScheduledPublishAtUtc { get; private set; }

    /// <summary>
    /// Set by <see cref="Retract"/>, cleared by <see cref="ReinstateRetracted"/>.
    /// While Retracted, <see cref="PublishedAtUtc"/> is deliberately KEPT: the
    /// original publication date is part of the public record the notice shows.
    /// </summary>
    public DateTimeOffset? RetractedAtUtc { get; private set; }
    public string? RetractedByUserId { get; private set; }

    /// <summary>
    /// The PUBLIC retraction text (max <see cref="RetractionNoticeMaxLength"/>).
    /// The internal reason is not stored here: it goes in the append-only
    /// EditorialArticleTransition row. Never null while Status is Retracted
    /// (also enforced by a database check constraint).
    /// </summary>
    public string? RetractionNotice { get; private set; }

    /// <summary>Set by <see cref="Archive"/>, cleared by <see cref="RestoreFromArchive"/>. <see cref="PublishedAtUtc"/> is kept.</summary>
    public DateTimeOffset? ArchivedAtUtc { get; private set; }
    public string? ArchivedByUserId { get; private set; }

    private EditorialArticle() { }

    public static EditorialArticle Create(
        string title, string? summary, string body, string ownerUserId, Guid? countryId, Guid? cityId)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            throw new ArgumentException("An EditorialArticle must have an owner.", nameof(ownerUserId));
        }

        var article = new EditorialArticle { OwnerUserId = ownerUserId };
        article.ApplyContent(title, summary, body, countryId, cityId);
        return article;
    }

    public void UpdateContent(string title, string? summary, string body, Guid? countryId, Guid? cityId)
    {
        ApplyContent(title, summary, body, countryId, cityId);
    }

    public void SetAuthorName(string? authorName)
    {
        var normalized = string.IsNullOrWhiteSpace(authorName) ? null : authorName.Trim();
        if (normalized is { Length: > 120 }) throw new ArgumentException("Author name cannot exceed 120 characters.", nameof(authorName));
        AuthorName = normalized;
    }

    public void UpdateSeo(string? seoTitle, string? seoDescription, string slug, string? canonicalUrl,
        string? socialTitle, string? socialDescription, string? socialImageUrl, bool noIndex, bool noFollow,
        string? twitterTitle = null, string? twitterDescription = null, string? twitterImageUrl = null)
    {
        var normalizedSlug = Slugify(slug);
        if (normalizedSlug.Length is < 1 or > 180) throw new ArgumentException("Slug must be 1–180 URL-safe characters.", nameof(slug));
        if (seoTitle is { Length: > 60 } || (seoTitle is not null && seoTitle.Length < 50))
            throw new ArgumentException("SEO title must be 50–60 characters when specified.", nameof(seoTitle));
        if (seoDescription is { Length: > 160 } || (seoDescription is not null && seoDescription.Length < 150))
            throw new ArgumentException("SEO description must be 150–160 characters when specified.", nameof(seoDescription));
        if (twitterTitle is { Length: > 60 } || (twitterTitle is not null && twitterTitle.Length < 50))
            throw new ArgumentException("Twitter title must be 50–60 characters when specified.", nameof(twitterTitle));
        if (twitterDescription is { Length: > 160 } || (twitterDescription is not null && twitterDescription.Length < 150))
            throw new ArgumentException("Twitter description must be 150–160 characters when specified.", nameof(twitterDescription));
        if (!string.IsNullOrWhiteSpace(canonicalUrl) && (!Uri.TryCreate(canonicalUrl, UriKind.Absolute, out var canonical) || canonical.Scheme is not ("https" or "http")))
            throw new ArgumentException("Canonical URL must be an absolute HTTP(S) URL.", nameof(canonicalUrl));
        if (!string.IsNullOrWhiteSpace(socialImageUrl) && (!Uri.TryCreate(socialImageUrl, UriKind.Absolute, out var image) || image.Scheme is not ("https" or "http")))
            throw new ArgumentException("Social image URL must be an absolute HTTP(S) URL.", nameof(socialImageUrl));
        if (!string.IsNullOrWhiteSpace(twitterImageUrl) && (!Uri.TryCreate(twitterImageUrl, UriKind.Absolute, out var twitterImage) || twitterImage.Scheme is not ("https" or "http")))
            throw new ArgumentException("Twitter image URL must be an absolute HTTP(S) URL.", nameof(twitterImageUrl));
        Slug = normalizedSlug;
        SeoTitle = string.IsNullOrWhiteSpace(seoTitle) ? null : seoTitle.Trim();
        SeoDescription = string.IsNullOrWhiteSpace(seoDescription) ? null : seoDescription.Trim();
        CanonicalUrl = string.IsNullOrWhiteSpace(canonicalUrl) ? null : canonicalUrl.Trim();
        SocialTitle = string.IsNullOrWhiteSpace(socialTitle) ? null : socialTitle.Trim();
        SocialDescription = string.IsNullOrWhiteSpace(socialDescription) ? null : socialDescription.Trim();
        SocialImageUrl = string.IsNullOrWhiteSpace(socialImageUrl) ? null : socialImageUrl.Trim();
        TwitterTitle = string.IsNullOrWhiteSpace(twitterTitle) ? null : twitterTitle.Trim();
        TwitterDescription = string.IsNullOrWhiteSpace(twitterDescription) ? null : twitterDescription.Trim();
        TwitterImageUrl = string.IsNullOrWhiteSpace(twitterImageUrl) ? null : twitterImageUrl.Trim();
        NoIndex = noIndex;
        NoFollow = noFollow;
    }

    public void UpdateTaxonomy(Guid? primaryCategoryId, IEnumerable<string>? secondaryTags)
    {
        var tags = (secondaryTags ?? Array.Empty<string>()).Select(t => t.Trim()).Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (tags.Count > 12 || tags.Any(t => t.Length > 40))
            throw new ArgumentException("Use up to 12 secondary tags, each no longer than 40 characters.", nameof(secondaryTags));
        PrimaryCategoryId = primaryCategoryId;
        SecondaryTags = tags;
    }

    public void SetPresentationDesks(IEnumerable<string>? deskSlugs)
    {
        var desks = (deskSlugs ?? Array.Empty<string>())
            .Select(slug => slug?.Trim() ?? string.Empty)
            .Where(slug => slug.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (desks.Count > 13 || desks.Any(slug => !EditorialDesk.IsValid(slug)))
            throw new ArgumentException("Select up to 13 valid presentation desks.", nameof(deskSlugs));
        PresentationDesks = EditorialDesk.Catalog.Keys.Where(desks.Contains).ToList();
    }

    public void SetFeaturedVideoMediaAsset(Guid? mediaAssetId)
    {
        FeaturedVideoMediaAssetId = mediaAssetId;
        if (mediaAssetId is null) HomeVideoOrder = null; // no video -> cannot stay in the video hero
    }

    public void SetHomeVideoOrder(int? order)
    {
        if (order is < 1) throw new ArgumentOutOfRangeException(nameof(order), "Order is 1-based.");
        HomeVideoOrder = order;
    }

    /// <summary>
    /// Sets the article's cover image (stored in <see cref="SocialImageUrl"/>, which the public feed,
    /// the article page and social cards all read). Null/blank removes it. Kept separate from
    /// <see cref="UpdateSeo"/> so a writer can attach a cover without SEO rights or a primary category.
    /// </summary>
    public void SetCoverImage(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            SocialImageUrl = null;
            return;
        }

        var trimmed = imageUrl.Trim();
        if (trimmed.Length > 2048 || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new ArgumentException("Cover image URL must be an absolute HTTP(S) URL.", nameof(imageUrl));
        SocialImageUrl = trimmed;
    }

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormKC);
        var chars = normalized.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return slug.Trim('-');
    }

    private static string InitialSlug(string title, Guid id)
    {
        var suffix = id.ToString("N")[..16];
        var stem = Slugify(title);
        if (stem.Length > 160) stem = stem[..160].TrimEnd('-');
        if (string.IsNullOrWhiteSpace(stem)) stem = "article";
        return $"{stem}-{suffix}";
    }

    /// <summary>Draft → InReview. The author (or an editor) hands the article over for editorial evaluation.</summary>
    public void SubmitForReview()
    {
        if (Status != EditorialArticleStatus.Draft)
        {
            throw new InvalidOperationException($"EditorialArticle {Id} is {Status}; only a Draft can be submitted for review.");
        }

        Status = EditorialArticleStatus.InReview;
    }

    /// <summary>InReview or Approved → Draft. Sends the article back to its author for changes.</summary>
    public void RequestRevision()
    {
        if (Status is not (EditorialArticleStatus.InReview or EditorialArticleStatus.Approved))
        {
            throw new InvalidOperationException($"EditorialArticle {Id} is {Status}; a revision can only be requested while it is InReview or Approved.");
        }

        Status = EditorialArticleStatus.Draft;
    }

    /// <summary>InReview → Approved.</summary>
    public void Approve()
    {
        if (Status != EditorialArticleStatus.InReview)
        {
            throw new InvalidOperationException($"EditorialArticle {Id} is {Status}; only an article InReview can be approved.");
        }

        Status = EditorialArticleStatus.Approved;
    }

    /// <summary>Approved → Scheduled. Scheduling never bypasses editorial approval.</summary>
    public void SchedulePublication(DateTimeOffset publishAtUtc, DateTimeOffset nowUtc)
    {
        if (Status != EditorialArticleStatus.Approved)
            throw new InvalidOperationException($"EditorialArticle {Id} is {Status}; only an Approved article can be scheduled.");
        if (publishAtUtc <= nowUtc)
            throw new ArgumentOutOfRangeException(nameof(publishAtUtc), "Scheduled publication must be in the future.");

        Status = EditorialArticleStatus.Scheduled;
        ScheduledPublishAtUtc = publishAtUtc.ToUniversalTime();
    }

    /// <summary>Scheduled → Approved. A cancelled item must be reviewed/scheduled again before publication.</summary>
    public void CancelScheduledPublication()
    {
        if (Status != EditorialArticleStatus.Scheduled)
            throw new InvalidOperationException($"EditorialArticle {Id} is {Status}; only a Scheduled article can be cancelled.");

        Status = EditorialArticleStatus.Approved;
        ScheduledPublishAtUtc = null;
    }

    /// <summary>Scheduled → Published. Called only by the trusted background publisher after the due time.</summary>
    public void PublishScheduled(string publisherId, DateTimeOffset nowUtc)
    {
        if (Status != EditorialArticleStatus.Scheduled || ScheduledPublishAtUtc is not { } dueAt || dueAt > nowUtc)
            throw new InvalidOperationException($"EditorialArticle {Id} is not due for scheduled publication.");
        if (string.IsNullOrWhiteSpace(publisherId))
            throw new ArgumentException("Publishing must be attributed to a user or system actor.", nameof(publisherId));

        Status = EditorialArticleStatus.Published;
        PublishedAtUtc = nowUtc;
        PublishedByUserId = publisherId;
        ScheduledPublishAtUtc = null;
    }

    /// <summary>Approved → InReview when its content changes; the prior approval no longer covers the edited text.</summary>
    public void RequireReapproval()
    {
        if (Status != EditorialArticleStatus.Approved)
        {
            throw new InvalidOperationException($"EditorialArticle {Id} is {Status}; only an Approved article can require reapproval.");
        }

        Status = EditorialArticleStatus.InReview;
    }

    /// <summary>
    /// Any non-published state → Published. The domain only guarantees the
    /// article is not already live; WHICH roles may publish from WHICH state
    /// (e.g. only the Editor-in-Chief straight from Draft) is a workflow
    /// authorization rule and lives in the application layer.
    /// </summary>
    public void Publish(string publishedByUserId, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(publishedByUserId))
        {
            throw new ArgumentException("Publishing must be attributed to a user.", nameof(publishedByUserId));
        }

        if (Status == EditorialArticleStatus.Published)
        {
            throw new InvalidOperationException($"EditorialArticle {Id} is already published.");
        }

        // Retracted and Archived leave only through their own transitions
        // (ReinstateRetracted / RestoreFromArchive). Publishing from them would
        // go live while still carrying a retraction notice or archive stamp.
        if (Status is EditorialArticleStatus.Retracted or EditorialArticleStatus.Archived)
        {
            throw new InvalidOperationException(
                $"EditorialArticle {Id} is {Status}; it cannot be published directly (use the dedicated restore/reinstate transition).");
        }

        Status = EditorialArticleStatus.Published;
        PublishedAtUtc = nowUtc;
        PublishedByUserId = publishedByUserId;
    }

    /// <summary>
    /// Returns a published article to Draft and clears its publication
    /// timestamp, so a later re-publish records a fresh publication time.
    /// </summary>
    public void Unpublish()
    {
        if (Status != EditorialArticleStatus.Published)
        {
            throw new InvalidOperationException($"EditorialArticle {Id} is not published.");
        }

        Status = EditorialArticleStatus.Draft;
        PublishedAtUtc = null;
        PublishedByUserId = null;
    }

    /// <summary>
    /// Published → Retracted. The article stops being public text: readers see
    /// only <paramref name="publicNotice"/>. Keeps <see cref="PublishedAtUtc"/>
    /// and <see cref="PublishedByUserId"/> (the original publication is part of
    /// the public record). The internal reason belongs on the transition row.
    /// </summary>
    public void Retract(string retractedByUserId, string publicNotice, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(retractedByUserId))
        {
            throw new ArgumentException("A retraction must be attributed to a user.", nameof(retractedByUserId));
        }

        if (string.IsNullOrWhiteSpace(publicNotice))
        {
            throw new ArgumentException("A retraction requires a public notice.", nameof(publicNotice));
        }

        if (publicNotice.Trim().Length > RetractionNoticeMaxLength)
        {
            throw new ArgumentException($"The retraction notice cannot exceed {RetractionNoticeMaxLength} characters.", nameof(publicNotice));
        }

        if (Status != EditorialArticleStatus.Published)
        {
            throw new InvalidOperationException($"EditorialArticle {Id} is {Status}; only a Published article can be retracted.");
        }

        Status = EditorialArticleStatus.Retracted;
        RetractedAtUtc = nowUtc;
        RetractedByUserId = retractedByUserId;
        RetractionNotice = publicNotice.Trim();
    }

    /// <summary>
    /// Published → Archived. Leaves the feeds but stays readable by URL.
    /// Keeps <see cref="PublishedAtUtc"/> (shown as "published on …").
    /// </summary>
    public void Archive(string archivedByUserId, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(archivedByUserId))
        {
            throw new ArgumentException("Archiving must be attributed to a user.", nameof(archivedByUserId));
        }

        if (Status != EditorialArticleStatus.Published)
        {
            throw new InvalidOperationException($"EditorialArticle {Id} is {Status}; only a Published article can be archived.");
        }

        Status = EditorialArticleStatus.Archived;
        ArchivedAtUtc = nowUtc;
        ArchivedByUserId = archivedByUserId;
    }

    /// <summary>
    /// Archived → Published, clearing the archive stamp. The original
    /// <see cref="PublishedAtUtc"/> is untouched, so the article returns to the
    /// feeds with its true publication time.
    /// </summary>
    public void RestoreFromArchive()
    {
        if (Status != EditorialArticleStatus.Archived)
        {
            throw new InvalidOperationException($"EditorialArticle {Id} is {Status}; only an Archived article can be restored.");
        }

        Status = EditorialArticleStatus.Published;
        ArchivedAtUtc = null;
        ArchivedByUserId = null;
    }

    /// <summary>
    /// Retracted → Draft. Clears the retraction AND the publication fields
    /// (like <see cref="Unpublish"/>), so a later publish records a fresh time.
    /// </summary>
    public void ReinstateRetracted()
    {
        if (Status != EditorialArticleStatus.Retracted)
        {
            throw new InvalidOperationException($"EditorialArticle {Id} is {Status}; only a Retracted article can be reinstated.");
        }

        Status = EditorialArticleStatus.Draft;
        RetractedAtUtc = null;
        RetractedByUserId = null;
        RetractionNotice = null;
        PublishedAtUtc = null;
        PublishedByUserId = null;
    }

    private void ApplyContent(string title, string? summary, string body, Guid? countryId, Guid? cityId)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("An EditorialArticle must have a non-empty title.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("An EditorialArticle must have a non-empty body.", nameof(body));
        }

        if (title.Trim().Length > TitleMaxLength)
        {
            throw new ArgumentException($"Title cannot exceed {TitleMaxLength} characters.", nameof(title));
        }

        if (summary is not null && summary.Trim().Length > SummaryMaxLength)
        {
            throw new ArgumentException($"Summary cannot exceed {SummaryMaxLength} characters.", nameof(summary));
        }

        if (body.Length > BodyMaxLength)
        {
            throw new ArgumentException($"Body cannot exceed {BodyMaxLength} characters.", nameof(body));
        }

        if (cityId.HasValue && !countryId.HasValue)
        {
            throw new ArgumentException("A city cannot be set without its country.", nameof(cityId));
        }

        Title = title.Trim();
        Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        Body = body.Trim();
        BodyFormat = ArticleBodyFormat.Html; // saving rewrites a legacy plain-text body as (sanitized) HTML
        CountryId = countryId;
        CityId = cityId;
        if (string.IsNullOrWhiteSpace(Slug)) Slug = InitialSlug(Title, Id);
    }
}
