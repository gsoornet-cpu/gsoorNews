using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Editorial.Contracts;
using Jusoor.Application.Stories.Contracts;
using Jusoor.Domain.Enums;
using Jusoor.Domain.Editorial;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Editorial;

// ---------- CMS (authenticated) ----------

public sealed record GetEditorialArticleQuery(
    Guid ArticleId, string ActorUserId, IReadOnlyList<string> ActorRoles) : IRequest<EditorialArticleResult>;

public class GetEditorialArticleQueryValidator : AbstractValidator<GetEditorialArticleQuery>
{
    public GetEditorialArticleQueryValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
    }
}

public class GetEditorialArticleQueryHandler : IRequestHandler<GetEditorialArticleQuery, EditorialArticleResult>
{
    private readonly IApplicationDbContext _context;

    public GetEditorialArticleQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<EditorialArticleResult> Handle(GetEditorialArticleQuery request, CancellationToken cancellationToken)
    {
        var article = await _context.EditorialArticles.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.ArticleId, cancellationToken);

        return article is null || !EditorialArticleAccess.CanView(request.ActorRoles, request.ActorUserId, article)
            ? new EditorialArticleResult(EditorialOutcome.NotFound, null)
            : new EditorialArticleResult(EditorialOutcome.Success, EditorialSupport.ToDto(article));
    }
}

public sealed record ListEditorialArticlesQuery(
    int Page, int PageSize, EditorialArticleStatus? Status, string ActorUserId, IReadOnlyList<string> ActorRoles)
    : IRequest<PagedResult<EditorialArticleSummaryDto>>;

public class ListEditorialArticlesQueryValidator : AbstractValidator<ListEditorialArticlesQuery>
{
    public ListEditorialArticlesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.ActorUserId).NotEmpty();
    }
}

public class ListEditorialArticlesQueryHandler : IRequestHandler<ListEditorialArticlesQuery, PagedResult<EditorialArticleSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public ListEditorialArticlesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PagedResult<EditorialArticleSummaryDto>> Handle(
        ListEditorialArticlesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.EditorialArticles.AsNoTracking();

        if (!EditorialArticleAccess.CanViewAll(request.ActorRoles))
        {
            // A Reporter sees only their own articles — enforced in the
            // query itself, not by the UI. Anyone else without view-all
            // rights sees nothing.
            var actorId = request.ActorUserId;
            query = request.ActorRoles.Contains(NewsroomRole.Reporter)
                ? query.Where(a => a.OwnerUserId == actorId)
                : query.Where(_ => false);
        }

        if (request.Status.HasValue)
        {
            var status = request.Status.Value;
            query = query.Where(a => a.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(a => a.LastModifiedAtUtc ?? a.CreatedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(a => new EditorialArticleSummaryDto(
            a.Id, a.Title, a.Summary, a.Status.ToString(), a.OwnerUserId,
            a.CreatedAtUtc, a.LastModifiedAtUtc, a.PublishedAtUtc)
        {
            ScheduledPublishAtUtc = a.ScheduledPublishAtUtc
        }).ToList();

        return new PagedResult<EditorialArticleSummaryDto>(items, request.Page, request.PageSize, totalCount);
    }
}

// ---------- Public (anonymous) ----------
//
// Visibility rules, in one place (Slice 20b, decision Q9):
//   feed (GetPublishedNewsQuery) ........ Published ONLY — this is what keeps Archived and Retracted out of every list.
//   detail (GetPublishedNewsByIdQuery) .. Published + Archived: full article · Retracted: the public notice only · anything else: not found.
//   sitemap ............................. Published + Archived (flagged) · Retracted never.

public sealed record GetPublishedNewsQuery(int Page = 1, int PageSize = 20, string? DeskSlug = null, string? SearchTerm = null) : IRequest<PagedResult<PublicNewsSummaryDto>>;

public class GetPublishedNewsQueryValidator : AbstractValidator<GetPublishedNewsQuery>
{
    public GetPublishedNewsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.DeskSlug).Must(slug => slug is null || EditorialDesk.IsValid(slug));
        RuleFor(x => x.SearchTerm).MaximumLength(120);
    }
}

public class GetPublishedNewsQueryHandler : IRequestHandler<GetPublishedNewsQuery, PagedResult<PublicNewsSummaryDto>>
{
    private const int ExcerptMaxLength = 220;

    private readonly IApplicationDbContext _context;

    public GetPublishedNewsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PagedResult<PublicNewsSummaryDto>> Handle(GetPublishedNewsQuery request, CancellationToken cancellationToken)
    {
        // The feed rule: Status == Published. Archived and Retracted are deliberately NOT here —
        // an archived article is still readable by URL, but it is no longer "news".
        var baseQuery = _context.EditorialArticles.AsNoTracking()
            .Where(a => a.Status == EditorialArticleStatus.Published && a.PublishedAtUtc != null);
        if (request.DeskSlug is not null)
        {
            baseQuery = baseQuery.Where(a => a.PresentationDesks.Contains(request.DeskSlug));
        }
        var searchTerm = request.SearchTerm?.Trim().ToLower();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            baseQuery = baseQuery.Where(a => a.Title.ToLower().Contains(searchTerm)
                || (a.Summary != null && a.Summary.ToLower().Contains(searchTerm))
                || a.Body.ToLower().Contains(searchTerm));
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var rows = await baseQuery
            .OrderByDescending(a => a.PublishedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var countryIds = rows.Where(a => a.CountryId.HasValue).Select(a => a.CountryId!.Value).Distinct().ToList();
        var cityIds = rows.Where(a => a.CityId.HasValue).Select(a => a.CityId!.Value).Distinct().ToList();
        var countries = await _context.Countries.Where(c => countryIds.Contains(c.Id)).AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
        var cities = await _context.Cities.Where(c => cityIds.Contains(c.Id)).AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);

        var items = rows.Select(a =>
        {
            var country = a.CountryId.HasValue && countries.TryGetValue(a.CountryId.Value, out var co) ? co : null;
            var city = a.CityId.HasValue && cities.TryGetValue(a.CityId.Value, out var ci) ? ci : null;
            // Plain text, never markup: Body is HTML now (Slice 21) and a teaser must not contain tags.
            var excerpt = a.Summary ?? ArticleBodyContent.Excerpt(a.Body, a.BodyFormat, ExcerptMaxLength);

            return new PublicNewsSummaryDto(
                a.Id, a.Title, excerpt, a.PublishedAtUtc!.Value,
                country?.NameAr, country?.NameEn, city?.NameAr, city?.NameEn)
                { Slug = a.Slug, PresentationDesks = a.PresentationDesks.ToArray(), ImageUrl = a.SocialImageUrl, AuthorName = a.AuthorName };
        }).ToList();

        return new PagedResult<PublicNewsSummaryDto>(items, request.Page, request.PageSize, totalCount);
    }
}

/// <param name="HomeOnly">True = only the videos an editor placed in the homepage hero, in the editor's order.</param>
public sealed record GetPublishedVideosQuery(int Page = 1, int PageSize = 18, bool HomeOnly = false)
    : IRequest<PagedResult<PublicVideoSummaryDto>>;

public sealed class GetPublishedVideosQueryValidator : AbstractValidator<GetPublishedVideosQuery>
{
    public GetPublishedVideosQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

/// <summary>Only published articles with an explicitly selected, ready video asset enter the public hub.</summary>
public sealed class GetPublishedVideosQueryHandler
    : IRequestHandler<GetPublishedVideosQuery, PagedResult<PublicVideoSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IEditorialMediaStorage _storage;

    public GetPublishedVideosQueryHandler(IApplicationDbContext context, IEditorialMediaStorage storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<PagedResult<PublicVideoSummaryDto>> Handle(
        GetPublishedVideosQuery request, CancellationToken cancellationToken)
    {
        var query = from article in _context.EditorialArticles.AsNoTracking()
                    join asset in _context.EditorialMediaAssets.AsNoTracking()
                        on article.FeaturedVideoMediaAssetId equals (Guid?)asset.Id
                    where article.Status == EditorialArticleStatus.Published
                          && article.PublishedAtUtc != null
                          && asset.Kind == EditorialMediaKind.Video
                          && asset.Status == EditorialMediaStatus.Ready
                    select new { Article = article, Asset = asset };

        // Homepage hero: ONLY editor-selected videos, in the editor's order. The video hub
        // (/videos) keeps listing every published video, newest first.
        var ordered = request.HomeOnly
            ? query.Where(r => r.Article.HomeVideoOrder != null)
                .OrderBy(r => r.Article.HomeVideoOrder).ThenByDescending(r => r.Article.PublishedAtUtc)
            : query.OrderBy(r => r.Article.HomeVideoOrder == null ? 1 : 0)
                .ThenBy(r => r.Article.HomeVideoOrder)
                .ThenByDescending(r => r.Article.PublishedAtUtc).ThenByDescending(r => r.Article.Id);

        var totalCount = await ordered.CountAsync(cancellationToken);
        var rows = await ordered.Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize).ToListAsync(cancellationToken);
        var items = rows.Select(row => new PublicVideoSummaryDto(
            row.Article.Id,
            row.Article.Title,
            row.Article.Summary ?? ArticleBodyContent.Excerpt(row.Article.Body, row.Article.BodyFormat, 220),
            _storage.GetPublicUrl(row.Asset.ObjectPath),
            row.Asset.Caption,
            row.Asset.Credit,
            row.Article.PublishedAtUtc!.Value,
            row.Article.Slug) { ThumbnailUrl = row.Article.SocialImageUrl, AuthorName = row.Article.AuthorName }).ToArray();
        return new PagedResult<PublicVideoSummaryDto>(items, request.Page, request.PageSize, totalCount);
    }
}

public sealed record GetPublishedNewsByIdQuery(Guid ArticleId) : IRequest<PublicNewsDetailResult>;

public class GetPublishedNewsByIdQueryValidator : AbstractValidator<GetPublishedNewsByIdQuery>
{
    public GetPublishedNewsByIdQueryValidator() => RuleFor(x => x.ArticleId).NotEmpty();
}

public class GetPublishedNewsByIdQueryHandler : IRequestHandler<GetPublishedNewsByIdQuery, PublicNewsDetailResult>
{
    private readonly IApplicationDbContext _context;

    public GetPublishedNewsByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PublicNewsDetailResult> Handle(GetPublishedNewsByIdQuery request, CancellationToken cancellationToken)
    {
        // 1) The article text is public only for Published and Archived. This is the hot path and
        //    stays a single query. Retracted is NOT matched here, so a retracted entity (and its
        //    body) is never even materialised on this path.
        var article = await _context.EditorialArticles.AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == request.ArticleId
                     && (a.Status == EditorialArticleStatus.Published || a.Status == EditorialArticleStatus.Archived)
                     && a.PublishedAtUtc != null,
                cancellationToken);

        if (article is null)
        {
            return await TryGetRetractionAsync(request.ArticleId, cancellationToken);
        }

        var country = article.CountryId.HasValue
            ? await _context.Countries.AsNoTracking().FirstOrDefaultAsync(c => c.Id == article.CountryId.Value, cancellationToken)
            : null;
        var city = article.CityId.HasValue
            ? await _context.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Id == article.CityId.Value, cancellationToken)
            : null;
        var category = article.PrimaryCategoryId.HasValue
            ? await _context.EditorialCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == article.PrimaryCategoryId.Value && c.IsActive, cancellationToken)
            : null;

        var publishedAtUtc = article.PublishedAtUtc!.Value;
        var isArchived = article.Status == EditorialArticleStatus.Archived;

        // Append order == issue order. Issuer id/roles are not projected: they never leave the CMS.
        // Corrections stay visible on an Archived article (CheckCorrection allows them there).
        var corrections = await _context.EditorialCorrections.AsNoTracking()
            .Where(c => c.ArticleId == article.Id)
            .OrderBy(c => c.IssuedAtUtc)
            .Select(c => new { c.Kind, c.Note, c.IsMajor, c.IssuedAtUtc })
            .ToListAsync(cancellationToken);

        var lastContentEditAtUtc = await _context.EditorialArticleRevisions.AsNoTracking()
            .Where(r => r.ArticleId == article.Id)
            .Select(r => (DateTimeOffset?)r.EditedAtUtc)
            .MaxAsync(cancellationToken);
        var lastCorrectionAtUtc = corrections.Count == 0 ? null : corrections.Max(c => (DateTimeOffset?)c.IssuedAtUtc);

        return PublicNewsDetailResult.Found(new PublicNewsDetailDto(
            article.Id, article.Title, article.Summary, ArticleBodyContent.ToHtml(article.Body, article.BodyFormat), publishedAtUtc,
            EditorialSupport.ToUpdatedAtUtc(publishedAtUtc, lastContentEditAtUtc, lastCorrectionAtUtc),
            country?.NameAr, country?.NameEn, city?.NameAr, city?.NameEn,
            corrections.Select(c => new PublicCorrectionDto(c.Kind.ToString(), c.Note, c.IsMajor, c.IssuedAtUtc)).ToList(),
            isArchived,
            isArchived ? article.ArchivedAtUtc : null)
        {
            Slug = article.Slug, SeoTitle = article.SeoTitle, SeoDescription = article.SeoDescription,
            AuthorName = article.AuthorName,
            CanonicalUrl = article.CanonicalUrl, SocialTitle = article.SocialTitle,
            SocialDescription = article.SocialDescription, SocialImageUrl = article.SocialImageUrl,
            NoIndex = article.NoIndex, NoFollow = article.NoFollow,
            PrimaryCategoryNameAr = category?.NameAr, SecondaryTags = article.SecondaryTags.ToArray(),
            PresentationDesks = article.PresentationDesks.ToArray(),
            TwitterTitle = article.TwitterTitle, TwitterDescription = article.TwitterDescription, TwitterImageUrl = article.TwitterImageUrl
        });
    }

    /// <summary>
    /// 2) Not a readable article — is it a retraction? Only the five public fields are
    /// SELECTed: the Body, Summary and geography columns are not even read from the database.
    /// A Retracted row that is somehow incomplete (no notice / no timestamps — the check
    /// constraint and the domain make that impossible, but this is the leak-sensitive path)
    /// fails CLOSED as not-found rather than showing a partial page.
    /// </summary>
    private async Task<PublicNewsDetailResult> TryGetRetractionAsync(Guid articleId, CancellationToken cancellationToken)
    {
        var row = await _context.EditorialArticles.AsNoTracking()
            .Where(a => a.Id == articleId
                        && a.Status == EditorialArticleStatus.Retracted
                        && a.PublishedAtUtc != null
                        && a.RetractedAtUtc != null
                        && a.RetractionNotice != null)
            .Select(a => new { a.Id, a.Title, a.PublishedAtUtc, a.RetractedAtUtc, a.RetractionNotice })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? PublicNewsDetailResult.NotFound()
            : PublicNewsDetailResult.Retracted(new PublicRetractionNoticeDto(
                row.Id, row.Title, row.PublishedAtUtc!.Value, row.RetractedAtUtc!.Value, row.RetractionNotice!));
    }
}

// ---------- Public sitemap feed (anonymous) — Published + Archived ----------

/// <summary>
/// Feeds the website's sitemap.xml and Google News sitemap (spec §16). Returns
/// every Published and Archived article, newest first, as lean rows with an
/// <c>IsArchived</c> flag: an archived article is still a live, readable URL (so
/// hiding it from sitemap.xml would silently de-index live journalism), but it
/// is not news (so the Google News sitemap must drop it). Retracted articles
/// are never listed.
/// </summary>
public sealed record GetPublishedNewsSitemapQuery : IRequest<IReadOnlyList<PublicNewsSitemapEntryDto>>;

public class GetPublishedNewsSitemapQueryHandler
    : IRequestHandler<GetPublishedNewsSitemapQuery, IReadOnlyList<PublicNewsSitemapEntryDto>>
{
    /// <summary>
    /// The sitemaps.org limit for a single sitemap file. If the newsroom ever
    /// publishes more than this, the oldest articles fall out of the sitemap —
    /// the point at which to introduce a sitemap index with paging (recorded as
    /// a known limitation; deliberately not built speculatively).
    /// </summary>
    public const int MaxEntries = 50_000;

    private readonly IApplicationDbContext _context;

    public GetPublishedNewsSitemapQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<PublicNewsSitemapEntryDto>> Handle(
        GetPublishedNewsSitemapQuery request, CancellationToken cancellationToken)
    {
        // Same readable-by-URL rule as the detail query: Published + Archived. Retracted is
        // excluded so its URL is never advertised to crawlers.
        // Project before materialising — an article body can be 100,000 characters
        // and a sitemap needs none of it.
        var rows = await _context.EditorialArticles.AsNoTracking()
            .Where(a => (a.Status == EditorialArticleStatus.Published || a.Status == EditorialArticleStatus.Archived)
                        && a.PublishedAtUtc != null && !a.NoIndex)
            .OrderByDescending(a => a.PublishedAtUtc)
            .ThenByDescending(a => a.Id) // deterministic order when two articles share a timestamp
            .Take(MaxEntries)
            .Select(a => new { a.Id, a.Title, a.Slug, a.Status, a.PublishedAtUtc })
            .ToListAsync(cancellationToken);

        var articleIds = rows.Select(r => r.Id).ToArray();
        var lastContentEdits = await _context.EditorialArticleRevisions.AsNoTracking()
            .Where(r => articleIds.Contains(r.ArticleId))
            .GroupBy(r => r.ArticleId)
            .Select(g => new { ArticleId = g.Key, AtUtc = g.Max(r => r.EditedAtUtc) })
            .ToDictionaryAsync(x => x.ArticleId, x => x.AtUtc, cancellationToken);
        var lastCorrections = await _context.EditorialCorrections.AsNoTracking()
            .Where(c => articleIds.Contains(c.ArticleId))
            .GroupBy(c => c.ArticleId)
            .Select(g => new { ArticleId = g.Key, AtUtc = g.Max(c => c.IssuedAtUtc) })
            .ToDictionaryAsync(x => x.ArticleId, x => x.AtUtc, cancellationToken);

        return rows
            .Select(r => new PublicNewsSitemapEntryDto(
                r.Id,
                r.Title,
                r.PublishedAtUtc!.Value,
                EditorialSupport.ToUpdatedAtUtc(
                    r.PublishedAtUtc!.Value,
                    lastContentEdits.TryGetValue(r.Id, out var editedAt) ? editedAt : null,
                    lastCorrections.TryGetValue(r.Id, out var correctedAt) ? correctedAt : null),
                r.Status == EditorialArticleStatus.Archived) { Slug = r.Slug })
            .ToList();
    }
}
