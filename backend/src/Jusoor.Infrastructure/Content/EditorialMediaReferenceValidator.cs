using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Jusoor.Infrastructure.Content;

/// <summary>
/// Resolves media IDs against ready assets before article HTML is persisted.
/// Client supplied URLs, captions and alternative text are never trusted.
/// </summary>
public sealed class EditorialMediaReferenceValidator(
    IApplicationDbContext context,
    IEditorialMediaStorage storage) : IEditorialMediaReferenceValidator
{
    public async Task<string?> ValidateAndCanonicalizeAsync(string html, CancellationToken cancellationToken)
    {
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(html, cancellationToken);
        foreach (var iframe in document.QuerySelectorAll("iframe").ToArray())
        {
            if (!TryNormalizeEmbed(iframe.GetAttribute("src"), out var source, out var provider)) return null;
            iframe.SetAttribute("src", source);
            iframe.SetAttribute("data-provider", provider);
            iframe.SetAttribute("title", $"محتوى مضمن من {provider}");
            iframe.SetAttribute("loading", "lazy");
            iframe.SetAttribute("referrerpolicy", "strict-origin-when-cross-origin");
            iframe.SetAttribute("sandbox", "allow-scripts allow-same-origin allow-presentation");
            iframe.SetAttribute("allow", "encrypted-media; picture-in-picture");
            iframe.SetAttribute("allowfullscreen", string.Empty);
            iframe.InnerHtml = string.Empty;
        }

        var mediaElements = document.QuerySelectorAll("img, video, a[data-media-id]").ToArray();
        var assetIds = new List<Guid>(mediaElements.Length);
        foreach (var element in mediaElements)
        {
            if (!Guid.TryParse(element.GetAttribute("data-media-id"), out var assetId)) return null;
            assetIds.Add(assetId);
        }

        var assets = await context.EditorialMediaAssets.AsNoTracking()
            .Where(x => assetIds.Contains(x.Id) && x.Status == EditorialMediaStatus.Ready)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var element in mediaElements)
        {
            if (!Guid.TryParse(element.GetAttribute("data-media-id"), out var id)) return null;
            if (!assets.TryGetValue(id, out var asset)) return null;

            if (element.LocalName is "img" or "video")
            {
                if ((element.LocalName == "img" && asset.Kind != EditorialMediaKind.Image)
                    || (element.LocalName == "video" && asset.Kind != EditorialMediaKind.Video)
                    || element.ParentElement?.LocalName != "figure") return null;
                element.SetAttribute("src", storage.GetPublicUrl(asset.ObjectPath));
                if (element.LocalName == "img") element.SetAttribute("alt", asset.AltText);
                else
                {
                    element.SetAttribute("controls", string.Empty);
                    element.SetAttribute("preload", "metadata");
                    element.RemoveAttribute("autoplay");
                }
                var figure = element.ParentElement;
                figure!.SetAttribute("data-media-id", id.ToString());
                var caption = figure.QuerySelector("figcaption");
                if (caption is null)
                {
                    caption = document.CreateElement("figcaption");
                    figure.AppendChild(caption);
                }
                caption.TextContent = element.LocalName == "img"
                    ? asset.AltText
                    : string.Join(" · ", new[] { asset.Caption, asset.Credit }.Where(x => !string.IsNullOrWhiteSpace(x)));
            }
            else
            {
                if (asset.Kind == EditorialMediaKind.Image) return null;
                element.SetAttribute("href", storage.GetPublicUrl(asset.ObjectPath));
                element.SetAttribute("rel", EditorialHtmlSanitizer.LinkRel);
                element.TextContent = $"{asset.Caption ?? asset.OriginalFileName} · {asset.Credit}";
            }
        }

        // A raw image/link URL without an asset ID is never an editorial media reference.
        if (document.QuerySelector("img:not([data-media-id])") is not null) return null;
        return document.Body?.InnerHtml ?? string.Empty;
    }

    internal static bool TryNormalizeEmbed(string? value, out string normalized, out string provider)
    {
        normalized = string.Empty;
        provider = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.Fragment)) return false;
        var host = uri.IdnHost.ToLowerInvariant();
        var path = uri.AbsolutePath.TrimEnd('/');
        if (host is "www.youtube-nocookie.com" or "youtube-nocookie.com" or "www.youtube.com" or "youtube.com" or "m.youtube.com")
        {
            var id = Regex.Match(path, @"^/(?:embed|shorts|live)/([A-Za-z0-9_-]{11})$").Groups[1].Value;
            if (id.Length == 0 && path == "/watch") id = GetSingleQuery(uri, "v");
            if (!Regex.IsMatch(id, @"^[A-Za-z0-9_-]{11}$")) return false;
            provider = "YouTube";
            normalized = $"https://www.youtube-nocookie.com/embed/{id}";
            return true;
        }
        else if (host == "youtu.be" && Regex.IsMatch(path, @"^/[A-Za-z0-9_-]{11}$"))
        {
            provider = "YouTube";
            normalized = $"https://www.youtube-nocookie.com/embed/{path[1..]}";
            return true;
        }
        else if (host == "player.vimeo.com" && Regex.IsMatch(path, @"^/video/[0-9]{1,20}$"))
            provider = "Vimeo";
        else if (host == "platform.twitter.com" && path == "/embed/Tweet.html"
                 && Regex.IsMatch(GetSingleQuery(uri, "id"), @"^[0-9]{1,30}$"))
            provider = "X";
        else if (host == "www.instagram.com" && Regex.IsMatch(path, @"^/(?:p|reel)/[A-Za-z0-9_-]{3,40}/embed$"))
            provider = "Instagram";
        else if (host == "www.facebook.com" && path == "/plugins/video.php" && IsFacebookLink(GetSingleQuery(uri, "href")))
            provider = "Facebook";
        else if (host == "www.tiktok.com" && Regex.IsMatch(path, @"^/embed/v2/[0-9]{5,30}$"))
            provider = "TikTok";
        else return false;

        normalized = uri.GetLeftPart(UriPartial.Path);
        if (provider == "X") normalized += "?id=" + Uri.EscapeDataString(GetSingleQuery(uri, "id"));
        if (provider == "Facebook") normalized += "?href=" + Uri.EscapeDataString(GetSingleQuery(uri, "href"));
        return true;
    }

    private static string GetSingleQuery(Uri uri, string name)
    {
        var values = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('=', 2)).Where(x => x.Length == 2 && Uri.UnescapeDataString(x[0]) == name)
            .Select(x => Uri.UnescapeDataString(x[1])).ToArray();
        return values.Length == 1 ? values[0] : string.Empty;
    }

    private static bool IsFacebookLink(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
           && uri.IdnHost.Equals("www.facebook.com", StringComparison.OrdinalIgnoreCase)
           && string.IsNullOrEmpty(uri.UserInfo) && uri.IsDefaultPort
           && (uri.AbsolutePath == "/watch" || uri.AbsolutePath.StartsWith("/videos/", StringComparison.Ordinal)
               || uri.AbsolutePath.StartsWith("/posts/", StringComparison.Ordinal)
               || uri.AbsolutePath.StartsWith("/reel/", StringComparison.Ordinal)
               || uri.AbsolutePath.StartsWith("/permalink.php", StringComparison.Ordinal));
}
