using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Jusoor.Domain.Enums;

namespace Jusoor.Application.Editorial;

/// <summary>
/// Pure helpers for reading an article body, whatever <see cref="ArticleBodyFormat"/>
/// it was stored in (Slice 21, decision D2). No I/O and no dependencies, so every
/// reader of <c>EditorialArticle.Body</c> — CMS, public page, history — goes
/// through the same two rules:
///
///  1. <see cref="ToHtml"/>: what the API returns. Always HTML. A legacy plain-text
///     body is HTML-ENCODED and wrapped in paragraphs (so a stored "&lt;script&gt;"
///     can never become markup), reproducing how the old page showed it
///     (<c>white-space: pre-line</c>: blank line = new paragraph, single newline = line break).
///  2. <see cref="ToPlainText"/>: excerpts and "does this body say anything?".
///     Output is TEXT, never markup; it is only ever placed in JSON and rendered
///     by React (which escapes it), so it does not need to be a perfect HTML parser.
/// </summary>
public static class ArticleBodyContent
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    // Tags that separate words even though no whitespace is written between them.
    private static readonly HashSet<string> WordBreakingTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "h1", "h2", "h3", "h4", "h5", "h6", "li", "ul", "ol", "blockquote", "div", "tr", "td", "br", "hr"
    };

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex ParagraphBreak = new(@"\n[ \t]*\n", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);

    /// <summary>The body as HTML. Html bodies are returned untouched (they were sanitized when written).</summary>
    public static string ToHtml(string body, ArticleBodyFormat format)
    {
        if (format == ArticleBodyFormat.Html)
        {
            return body;
        }

        var normalized = body.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var paragraphs = ParagraphBreak.Split(normalized)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Select(p => "<p>" + WebUtility.HtmlEncode(p).Replace("\n", "<br>") + "</p>");

        return string.Concat(paragraphs);
    }

    /// <summary>The visible text of the body: tags removed, entities decoded, whitespace collapsed.</summary>
    public static string ToPlainText(string body, ArticleBodyFormat format)
    {
        if (format == ArticleBodyFormat.PlainText)
        {
            return Whitespace.Replace(body, " ").Trim();
        }

        return ToPlainText(body);
    }

    /// <summary>Same as the two-argument overload for a body already known to be HTML.</summary>
    public static string ToPlainText(string html)
        => Whitespace.Replace(WebUtility.HtmlDecode(StripTags(html)), " ").Trim();

    /// <summary>
    /// Removes tags in a single linear pass (a hand-written scanner rather than a
    /// regular expression, so no input can make it backtrack or time out — it runs on
    /// the public list endpoint). Tolerates "&gt;" inside a quoted attribute value.
    /// A "&lt;" that does not start a tag ("a &lt; b") is kept as text. An unterminated
    /// tag drops the rest of the input. HTML comments are not specially handled:
    /// sanitized bodies never contain them.
    /// </summary>
    private static string StripTags(string html)
    {
        var text = new StringBuilder(html.Length);
        var i = 0;

        while (i < html.Length)
        {
            if (html[i] != '<' || i + 1 >= html.Length || !IsTagStart(html[i + 1]))
            {
                text.Append(html[i]);
                i++;
                continue;
            }

            var j = i + 1;
            if (html[j] == '/')
            {
                j++;
            }

            var nameStart = j;
            while (j < html.Length && char.IsAsciiLetterOrDigit(html[j]))
            {
                j++;
            }

            var name = html.Substring(nameStart, j - nameStart);

            var quote = '\0';
            for (; j < html.Length; j++)
            {
                var c = html[j];
                if (quote != '\0')
                {
                    if (c == quote)
                    {
                        quote = '\0';
                    }
                }
                else if (c == '"' || c == '\'')
                {
                    quote = c;
                }
                else if (c == '>')
                {
                    break;
                }
            }

            if (WordBreakingTags.Contains(name))
            {
                text.Append(' ');
            }

            i = j + 1;
        }

        return text.ToString();
    }

    private static bool IsTagStart(char c) => char.IsAsciiLetter(c) || c is '/' or '!' or '?';

    /// <summary>True when the HTML contains at least one visible character (an empty editor emits "&lt;p&gt;&lt;/p&gt;").</summary>
    public static bool HasText(string html) => ToPlainText(html).Length > 0;

    /// <summary>A plain-text teaser of at most <paramref name="maxLength"/> characters plus an ellipsis.</summary>
    public static string Excerpt(string body, ArticleBodyFormat format, int maxLength)
    {
        var text = ToPlainText(body, format);
        if (text.Length <= maxLength)
        {
            return text;
        }

        var cut = maxLength;
        if (char.IsHighSurrogate(text[cut - 1]))
        {
            cut--; // never split a surrogate pair
        }

        return text[..cut].TrimEnd() + "…";
    }
}
