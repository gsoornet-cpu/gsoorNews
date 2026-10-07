namespace Jusoor.Domain.Enums;

/// <summary>
/// How an editorial article's stored <c>Body</c> must be interpreted.
///
/// <see cref="PlainText"/> is deliberately the ZERO value: every row written
/// before Slice 21 (decision D2, rich text) holds plain text, and the EF
/// migration that adds the column back-fills existing rows with 0. Making the
/// legacy format the zero value means that back-fill needs no hand-edited
/// default. The values are persisted as an int, so they are pinned by a test.
///
/// Every row written from Slice 21 on is <see cref="Html"/>: allow-list
/// sanitized on the server before it is stored.
/// </summary>
public enum ArticleBodyFormat
{
    /// <summary>Legacy plain text. Never rendered as markup: it is HTML-encoded on read.</summary>
    PlainText = 0,

    /// <summary>HTML that has already passed the server-side allow-list sanitizer.</summary>
    Html = 1
}
