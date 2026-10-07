namespace Jusoor.Application.Common.Interfaces;

/// <summary>
/// Allow-list HTML sanitizer for editorial article bodies (decision D2: "store
/// clean HTML; sanitize server-side — never trust the editor").
///
/// This is the single trust boundary for article markup: whatever the CMS
/// editor, a script or a hand-crafted request sends, only the output of this
/// service is ever stored, and the public site renders the stored value as
/// markup. Implementations must be deterministic and idempotent
/// (<c>Sanitize(Sanitize(x)) == Sanitize(x)</c>) so re-saving an article never
/// drifts its content.
/// </summary>
public interface IEditorialHtmlSanitizer
{
    /// <summary>
    /// Returns <paramref name="html"/> reduced to the editorial allow-list: no
    /// scripts, event handlers, styles, classes, forms, embeds or images, links
    /// restricted to http(s)/mailto, and every link carrying
    /// <c>rel="noopener noreferrer"</c>. Never throws for malformed markup.
    /// </summary>
    string Sanitize(string html);
}

/// <summary>Rebinds media IDs in sanitized article HTML to newsroom-owned, ready assets.</summary>
public interface IEditorialMediaReferenceValidator
{
    Task<string?> ValidateAndCanonicalizeAsync(string html, CancellationToken cancellationToken);
}
