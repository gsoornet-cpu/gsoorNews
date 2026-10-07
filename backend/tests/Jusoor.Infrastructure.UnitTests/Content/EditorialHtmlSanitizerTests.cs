using AngleSharp.Html.Parser;
using FluentAssertions;
using Jusoor.Infrastructure.Content;
using Xunit;

namespace Jusoor.Infrastructure.UnitTests.Content;

/// <summary>
/// Slice 21, decision D2: the server-side allow-list is the only thing standing between an
/// editor's (or an attacker's) markup and the public site, so this is tested against the real
/// Ganss.Xss library rather than a stub. Assertions about what must be GONE are deliberately
/// substring-based (no script, no handler, no scheme) so they do not depend on how the library
/// happens to serialize the rest.
/// </summary>
public class EditorialHtmlSanitizerTests
{
    private static readonly EditorialHtmlSanitizer Sut = new();

    // ---------------- what editors can produce survives ----------------

    [Theory]
    [InlineData("<p>نص</p>", "<p>نص</p>")]
    [InlineData("<h2>عنوان</h2>", "<h2>عنوان</h2>")]
    [InlineData("<h3>عنوان فرعي</h3>", "<h3>عنوان فرعي</h3>")]
    [InlineData("<p><strong>غامق</strong> <em>مائل</em> <u>مسطّر</u></p>", "<p><strong>غامق</strong> <em>مائل</em> <u>مسطّر</u></p>")]
    [InlineData("<blockquote><p>اقتباس</p></blockquote>", "<blockquote><p>اقتباس</p></blockquote>")]
    [InlineData("<ul><li>أ</li><li>ب</li></ul>", "<ul><li>أ</li><li>ب</li></ul>")]
    [InlineData("<ol><li>أ</li></ol>", "<ol><li>أ</li></ol>")]
    [InlineData("<p>سطر<br>آخر</p><hr>", "<p>سطر<br>آخر</p><hr>")]
    [InlineData("<table><thead><tr><th>العنوان</th></tr></thead><tbody><tr><td>البيان</td></tr></tbody></table>", "<table><thead><tr><th>العنوان</th></tr></thead><tbody><tr><td>البيان</td></tr></tbody></table>")]
    [InlineData("<table><tbody><tr><th>العنوان</th><th>القيمة</th></tr><tr><td>أ</td><td>ب</td></tr></tbody></table>", "<table><tbody><tr><th>العنوان</th><th>القيمة</th></tr><tr><td>أ</td><td>ب</td></tr></tbody></table>")]
    public void Allowed_editor_markup_should_pass_through_unchanged(string input, string expected)
        => Sut.Sanitize(input).Should().Be(expected);

    [Fact]
    public void A_body_without_any_tags_should_come_back_unchanged_so_existing_clients_keep_working()
    {
        const string plain = "نص الخبر الكامل هنا.";

        Sut.Sanitize(plain).Should().Be(plain);
    }

    [Fact]
    public void A_stray_less_than_sign_in_text_should_be_encoded_not_dropped()
        => Sut.Sanitize("a < b").Should().Contain("a &lt; b");

    [Fact]
    public void Empty_input_should_stay_empty()
        => Sut.Sanitize(string.Empty).Should().BeEmpty();

    // ---------------- links ----------------

    [Fact]
    public void An_https_link_should_keep_its_href_and_gain_rel_noopener_noreferrer()
    {
        var html = Sut.Sanitize("<p><a href=\"https://example.com/a\">رابط</a></p>");

        html.Should().Contain("href=\"https://example.com/a\"");
        html.Should().Contain("rel=\"noopener noreferrer\"");
    }

    [Fact]
    public void Mailto_and_relative_links_should_be_allowed_and_get_rel_too()
    {
        var html = Sut.Sanitize("<p><a href=\"mailto:desk@example.com\">بريد</a> <a href=\"/article/1\">داخلي</a></p>");

        html.Should().Contain("href=\"mailto:desk@example.com\"");
        html.Should().Contain("href=\"/article/1\"");
        html.Split("rel=\"noopener noreferrer\"").Length.Should().Be(3, "both links carry the rel");
    }

    [Fact]
    public void A_rel_or_target_supplied_by_the_client_should_never_be_trusted()
    {
        var html = Sut.Sanitize("<p><a href=\"https://example.com\" target=\"_blank\" rel=\"opener\">x</a></p>");

        html.Should().NotContain("target");
        html.Should().NotContain("rel=\"opener\"");
        html.Should().Contain("rel=\"noopener noreferrer\"");
    }

    [Fact]
    public void An_approved_provider_embed_is_normalized_and_sandboxed()
    {
        var html = Sut.Sanitize("<iframe src=\"https://www.youtube-nocookie.com/embed/abcdefghijk?autoplay=1\" allow=\"camera\"></iframe>");
        html.Should().Contain("src=\"https://www.youtube-nocookie.com/embed/abcdefghijk\"");
        html.Should().Contain("sandbox=\"allow-scripts allow-same-origin allow-presentation\"");
        html.Should().Contain("loading=\"lazy\"");
        html.Should().NotContain("autoplay");
        html.Should().NotContain("camera");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("  javascript:alert(1)")]
    [InlineData("jav&#x61;script:alert(1)")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    public void Dangerous_url_schemes_should_lose_the_link_target_and_get_no_rel(string href)
    {
        var html = Sut.Sanitize($"<p><a href=\"{href}\">x</a></p>");

        html.Should().NotContain("href");
        html.Should().NotContain("alert");
        html.Should().NotContain("data:");
        html.Should().NotContain("rel=");
        html.Should().Contain("x");
    }

    // ---------------- everything else is removed ----------------

    [Theory]
    [InlineData("<p>a</p><script>alert(1)</script>", "alert")]
    [InlineData("<p>a</p><style>p{color:red}</style>", "color")]
    [InlineData("<p>a</p><iframe src=\"https://evil.example\"></iframe>", "iframe")]
    [InlineData("<p>a</p><form action=\"/x\"><input name=\"q\"></form>", "form")]
    [InlineData("<p>a</p><img src=\"x\" onerror=\"alert(1)\">", "onerror")]
    [InlineData("<p>a</p><svg onload=\"alert(1)\"><circle/></svg>", "onload")]
    [InlineData("<p>a</p><object data=\"x\"></object><embed src=\"x\">", "embed")]
    [InlineData("<p>a</p><video src=\"x\" onerror=\"alert(1)\"></video>", "video")]
    [InlineData("<p>a</p><math><mi xlink:href=\"javascript:alert(1)\">x</mi></math>", "javascript")]
    [InlineData("<p>a</p><!-- hidden <script>alert(1)</script> -->", "<!--")]
    public void Disallowed_elements_should_be_removed_completely(string input, string mustNotRemain)
    {
        var html = Sut.Sanitize(input);

        html.Should().NotContain(mustNotRemain);
        html.Should().Contain("<p>a</p>");
    }

    [Theory]
    [InlineData("<p onclick=\"alert(1)\">a</p>", "onclick")]
    [InlineData("<p onmouseover=alert(1)>a</p>", "onmouseover")]
    [InlineData("<p style=\"background:url(javascript:alert(1))\">a</p>", "style")]
    [InlineData("<p class=\"x\" id=\"y\" dir=\"ltr\" lang=\"en\">a</p>", "class")]
    [InlineData("<p data-x=\"1\">a</p>", "data-x")]
    [InlineData("<p title=\"t\" contenteditable=\"true\">a</p>", "contenteditable")]
    public void Attributes_other_than_href_should_be_removed_from_allowed_elements(string input, string mustNotRemain)
    {
        var html = Sut.Sanitize(input);

        html.Should().Be("<p>a</p>");
        html.Should().NotContain(mustNotRemain);
    }

    [Fact]
    public void Tables_should_keep_only_simple_cells_without_merge_attributes_or_styles()
    {
        var html = Sut.Sanitize("<table><thead><tr><th colspan=\"2\" rowspan=\"3\" style=\"color:red\">x</th></tr></thead></table>");

        html.Should().Contain("<th>x</th>");
        html.Should().NotContain("colspan");
        html.Should().NotContain("rowspan");
        html.Should().NotContain("style=");
    }

    [Fact]
    public void Tables_without_a_simple_header_row_or_with_nested_tables_should_not_survive()
    {
        var missingHeader = Sut.Sanitize("<table><tbody><tr><td>no header</td></tr></tbody></table>");
        var nested = Sut.Sanitize("<table><thead><tr><th>Header</th></tr></thead><tbody><tr><td>outer<table><thead><tr><th>nested</th></tr></thead></table></td></tr></tbody></table>");

        missingHeader.Should().NotContain("<table");
        nested.Should().Contain("<table");
        nested.Should().NotContain("nested");
    }

    [Theory]
    [InlineData("<noscript><p title=\"</noscript><img src=x onerror=alert(1)>\"></noscript>")]
    [InlineData("<svg><desc><![CDATA[</desc><script>alert(1)</script>]]></desc></svg>")]
    [InlineData("<math><mtext><table><mglyph><style><!--</style><img title=\"--&gt;&lt;img src=1 onerror=alert(1)&gt;\">")]
    [InlineData("<p><<script>alert(1)//<</script>/p>")]
    [InlineData("<a href=\"x\" onclick=\"alert(1)\"><b onmouseover=alert(1)>x</b></a>")]
    [InlineData("<scr<script>ipt>alert(1)</scr</script>ipt>")]
    [InlineData("<img src=\"x\" onerror=\"alert(1)\" />")]
    [InlineData("<body onload=alert(1)><p>a</p></body>")]
    // CVE-2026-54570 / GHSA-pgww-w46g-26qg: mutation XSS through a MathML <annotation-xml> HTML integration
    // point, fixed in AngleSharp 1.5.0. Regression guard for the parser the sanitizer relies on: if the
    // package ever resolves back to a vulnerable AngleSharp, or the allow-list ever admits <math>, these fail.
    [InlineData("<math><annotation-xml encoding=\"text/html\"><a title=\"</annotation-xml><img src=x onerror=alert(1)>\">x</a></annotation-xml></math>")]
    [InlineData("<p>a</p><math><mtext><annotation-xml encoding=\"application/xhtml+xml\"><p title=\"</annotation-xml><script>alert(1)</script>\">x</p></annotation-xml></mtext></math>")]
    public void Known_xss_payloads_should_leave_only_allow_listed_markup(string payload)
    {
        var html = Sut.Sanitize(payload);

        // Structural, not textual: harmless text such as an HTML-encoded "onerror" may legitimately
        // survive as text; what must never survive is an element or attribute outside the allow-list.
        AssertOnlyAllowListedMarkup(html);
        html.Should().NotContainEquivalentOf("<script");
    }

    private static void AssertOnlyAllowListedMarkup(string html)
    {
        var document = new HtmlParser().ParseDocument("<body>" + html + "</body>");

        foreach (var element in document.Body!.QuerySelectorAll("*"))
        {
            EditorialHtmlSanitizer.AllowedTags.Should().Contain(element.LocalName, "element <{0}> is not on the allow-list", element.LocalName);

            foreach (var attribute in element.Attributes)
            {
                (attribute.Name == "rel" || EditorialHtmlSanitizer.AllowedAttributes.Contains(attribute.Name))
                    .Should().BeTrue("attribute {0} on <{1}> is not on the allow-list", attribute.Name, element.LocalName);
            }

            if (element.GetAttribute("href") is { } href && Uri.TryCreate(href.Trim(), UriKind.Absolute, out var uri))
            {
                EditorialHtmlSanitizer.AllowedSchemes.Should().Contain(uri.Scheme);
            }
        }
    }

    // ---------------- stability ----------------

    [Fact]
    public void Sanitizing_twice_should_give_the_same_result_as_sanitizing_once()
    {
        const string input =
            "<h2 class=\"x\">عنوان</h2><p onclick=\"x()\">نص <strong>غامق</strong> و<a href=\"https://example.com\" rel=\"opener\">رابط</a></p>"
            + "<script>alert(1)</script><ul><li>أ</li></ul><p>a < b & c</p>";

        var once = Sut.Sanitize(input);

        Sut.Sanitize(once).Should().Be(once);
    }

    [Fact]
    public void The_allow_list_should_be_exactly_what_the_editor_can_produce()
    {
        EditorialHtmlSanitizer.AllowedTags.Should().BeEquivalentTo(
            "p", "br", "h2", "h3", "strong", "em", "u", "blockquote", "ul", "ol", "li", "a", "hr", "figure", "figcaption", "img", "video", "iframe", "table", "thead", "tbody", "tr", "th", "td");
        EditorialHtmlSanitizer.AllowedAttributes.Should().BeEquivalentTo("href", "src", "alt", "title", "data-media-id", "data-provider", "sandbox", "allow", "allowfullscreen", "loading", "referrerpolicy", "controls", "preload");
        EditorialHtmlSanitizer.AllowedSchemes.Should().BeEquivalentTo("http", "https", "mailto");
    }

    [Fact]
    public void It_should_be_safe_to_call_from_many_threads_at_once()
    {
        var results = new string[64];

        Parallel.For(0, results.Length, i =>
            results[i] = Sut.Sanitize($"<p onclick=\"x()\">{i}</p><script>alert({i})</script>"));

        for (var i = 0; i < results.Length; i++)
        {
            results[i].Should().Be($"<p>{i}</p>");
        }
    }
}
