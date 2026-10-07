using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Editorial.Contracts;
using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Jusoor.Domain.Enums;
using System.Text.RegularExpressions;

namespace Jusoor.Application.Editorial;

public enum EditorialOutcome
{
    Success,
    NotFound,      // missing OR not visible to the caller — deliberately indistinguishable (no id enumeration)
    Forbidden,     // caller can see the article but may not perform this action
    InvalidGeography,
    Conflict,      // e.g. publishing an already-published article
    InvalidBody,    // body is empty once sanitized (e.g. "<p></p>"), or too long once sanitized
    InvalidFeaturedVideo,
    InvalidEditReason,
    InvalidScheduleTime
}

public sealed record EditorialArticleResult(EditorialOutcome Outcome, EditorialArticleDto? Article)
{
    public bool Succeeded => Outcome == EditorialOutcome.Success;
}

internal static class EditorialSupport
{
    public static EditorialArticleDto ToDto(EditorialArticle a) => new(
        a.Id, a.Title, a.Summary, ArticleBodyContent.ToHtml(a.Body, a.BodyFormat), a.Status.ToString(), a.CountryId, a.CityId, a.OwnerUserId,
        a.CreatedAtUtc, a.LastModifiedAtUtc, a.PublishedAtUtc)
    {
        ScheduledPublishAtUtc = a.ScheduledPublishAtUtc,
        AuthorName = a.AuthorName,
        Slug = a.Slug, SeoTitle = a.SeoTitle, SeoDescription = a.SeoDescription, CanonicalUrl = a.CanonicalUrl,
        SocialTitle = a.SocialTitle, SocialDescription = a.SocialDescription, SocialImageUrl = a.SocialImageUrl,
        NoIndex = a.NoIndex, NoFollow = a.NoFollow, PrimaryCategoryId = a.PrimaryCategoryId,
        SecondaryTags = a.SecondaryTags.ToArray(),
        PresentationDesks = a.PresentationDesks.ToArray(),
        FeaturedVideoMediaAssetId = a.FeaturedVideoMediaAssetId,
        TwitterTitle = a.TwitterTitle, TwitterDescription = a.TwitterDescription, TwitterImageUrl = a.TwitterImageUrl
    };

    /// <summary>
    /// A SANITIZED body is acceptable when it still says something (an empty
    /// editor emits "&lt;p&gt;&lt;/p&gt;", which the plain NotEmpty rule cannot
    /// see) and still fits the stored limit (sanitizing can lengthen markup,
    /// e.g. by adding rel="noopener noreferrer" to every link). Rejecting here
    /// returns a 400; letting the domain throw would surface as a 500.
    /// </summary>
    public static bool IsBodyAcceptable(string sanitizedBody)
        => sanitizedBody.Length <= EditorialArticle.BodyMaxLength && ArticleBodyContent.HasText(sanitizedBody);

    public static async Task<bool> IsFeaturedVideoValidAsync(
        IApplicationDbContext context, string body, Guid? mediaAssetId, CancellationToken cancellationToken)
    {
        if (mediaAssetId is null) return true;
        var id = mediaAssetId.Value;
        var pattern = $"<video\\b(?=[^>]*\\bdata-media-id=\"{Regex.Escape(id.ToString())}\")[^>]*>";
        if (!Regex.IsMatch(body, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
        return await context.EditorialMediaAssets.AnyAsync(asset => asset.Id == id
            && asset.Kind == EditorialMediaKind.Video && asset.Status == EditorialMediaStatus.Ready, cancellationToken);
    }

    public static EditorialCorrectionDto ToDto(EditorialCorrection c) => new(
        c.Id, c.Kind.ToString(), c.Note, c.IsMajor, c.IssuedByUserId, c.IssuerRoles, c.IssuedAtUtc);

    /// <summary>
    /// Public dateModified reflects publication, a recorded content revision or
    /// a public correction. Workflow-only changes such as archive/restore do not
    /// make the story appear to have changed. Shared by article detail and sitemaps.
    /// </summary>
    public static DateTimeOffset ToUpdatedAtUtc(
        DateTimeOffset publishedAtUtc, DateTimeOffset? lastContentEditAtUtc, DateTimeOffset? lastCorrectionAtUtc)
    {
        var latest = publishedAtUtc;
        if (lastContentEditAtUtc is { } edit && edit > latest) latest = edit;
        if (lastCorrectionAtUtc is { } correction && correction > latest) latest = correction;
        return latest;
    }

    /// <summary>
    /// Country/city references must exist, and a city must belong to the
    /// stated country. (The consistency check is new here — the same gap in
    /// UpdateProfileLocationCommand is recorded as existing tech debt.)
    /// </summary>
    public static async Task<bool> IsGeographyValidAsync(
        IApplicationDbContext context, Guid? countryId, Guid? cityId, CancellationToken cancellationToken)
    {
        if (countryId is null)
        {
            return cityId is null;
        }

        if (!await context.Countries.AnyAsync(c => c.Id == countryId.Value, cancellationToken))
        {
            return false;
        }

        if (cityId is null)
        {
            return true;
        }

        return await context.Cities.AnyAsync(c => c.Id == cityId.Value && c.CountryId == countryId.Value, cancellationToken);
    }
}
