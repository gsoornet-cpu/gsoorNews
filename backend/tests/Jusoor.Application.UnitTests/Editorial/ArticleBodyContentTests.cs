using FluentAssertions;
using Jusoor.Application.Editorial;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Application.UnitTests.Editorial;

/// <summary>Slice 21: reading an article body whatever format it was stored in.</summary>
public class ArticleBodyContentTests
{
    // ---------------- ToHtml ----------------

    [Fact]
    public void ToHtml_should_return_an_html_body_untouched()
        => ArticleBodyContent.ToHtml("<p>x</p>", ArticleBodyFormat.Html).Should().Be("<p>x</p>");

    [Theory]
    [InlineData("a\n\nb", "<p>a</p><p>b</p>")]
    [InlineData("l1\nl2", "<p>l1<br>l2</p>")]
    [InlineData("a\r\n\r\nb\r\nc", "<p>a</p><p>b<br>c</p>")]
    [InlineData("a\n  \nb", "<p>a</p><p>b</p>")]
    [InlineData("نص الخبر", "<p>نص الخبر</p>")]
    [InlineData("  \n ", "")]
    public void ToHtml_should_turn_legacy_plain_text_into_paragraphs_like_the_old_pre_line_page(string plain, string expected)
        => ArticleBodyContent.ToHtml(plain, ArticleBodyFormat.PlainText).Should().Be(expected);

    [Fact]
    public void ToHtml_should_encode_markup_in_legacy_plain_text_so_it_can_never_become_markup()
    {
        ArticleBodyContent.ToHtml("<script>alert(1)</script>", ArticleBodyFormat.PlainText)
            .Should().Be("<p>&lt;script&gt;alert(1)&lt;/script&gt;</p>");
        ArticleBodyContent.ToHtml("a \"b\" & c", ArticleBodyFormat.PlainText)
            .Should().Be("<p>a &quot;b&quot; &amp; c</p>");
    }

    // ---------------- ToPlainText ----------------

    [Theory]
    [InlineData("<p>Hello <strong>wor</strong>ld</p>", "Hello world")]
    [InlineData("<p>one</p><p>two</p>", "one two")]
    [InlineData("a<br>b<br/>c", "a b c")]
    [InlineData("<ul><li>a</li><li>b</li></ul>", "a b")]
    [InlineData("<p>a &amp; b &lt;c&gt; &quot;d&quot;</p>", "a & b <c> \"d\"")]
    [InlineData("<p><a href=\"/x?a>b\">link</a> tail</p>", "link tail")]
    [InlineData("<p></p>", "")]
    [InlineData("<p>&nbsp;</p>", "")]
    [InlineData("<p>a \n  b\t c</p>", "a b c")]
    [InlineData("<h2>عنوان</h2><p>نص <em>الخبر</em></p>", "عنوان نص الخبر")]
    [InlineData("<P>one</P><P>two</P>", "one two")]
    [InlineData("a<br />b", "a b")]
    [InlineData("<p>a < b and c > d</p>", "a < b and c > d")]
    public void ToPlainText_should_return_only_the_visible_text(string html, string expected)
        => ArticleBodyContent.ToPlainText(html).Should().Be(expected);

    [Fact]
    public void ToPlainText_should_treat_encoded_markup_as_text_not_as_a_tag()
        => ArticleBodyContent.ToPlainText("<p>&lt;script&gt;x&lt;/script&gt;</p>").Should().Be("<script>x</script>");

    [Fact]
    public void ToPlainText_should_drop_an_unterminated_tag_and_finish_quickly_on_hostile_input()
    {
        var hostile = string.Concat(Enumerable.Repeat("<a b=\"", 20_000));

        var started = DateTime.UtcNow;
        var text = ArticleBodyContent.ToPlainText(hostile);

        text.Should().BeEmpty();
        (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(2));
        ArticleBodyContent.ToPlainText("<p>ok</p><a href=\"x>tail").Should().Be("ok");
    }

    [Fact]
    public void ToPlainText_for_a_legacy_body_should_keep_angle_brackets_because_they_are_just_text()
        => ArticleBodyContent.ToPlainText("a <b>c</b>\n\nd", ArticleBodyFormat.PlainText).Should().Be("a <b>c</b> d");

    // ---------------- HasText ----------------

    [Theory]
    [InlineData("<p></p>", false)]
    [InlineData("<p><br></p><hr>", false)]
    [InlineData("<p>&nbsp;</p>", false)]
    [InlineData("<p>x</p>", true)]
    [InlineData("x", true)]
    public void HasText_should_see_through_empty_editor_output(string html, bool expected)
        => ArticleBodyContent.HasText(html).Should().Be(expected);

    // ---------------- Excerpt ----------------

    [Theory]
    [InlineData("<p>abc</p>", 10, "abc")]
    [InlineData("<p>abcdefghij</p>", 5, "abcde…")]
    [InlineData("<p>ab cd ef</p>", 3, "ab…")]
    public void Excerpt_should_be_plain_text_cut_at_the_limit(string html, int max, string expected)
        => ArticleBodyContent.Excerpt(html, ArticleBodyFormat.Html, max).Should().Be(expected);

    [Fact]
    public void Excerpt_should_never_split_a_surrogate_pair()
        => ArticleBodyContent.Excerpt("<p>ab😀cd</p>", ArticleBodyFormat.Html, 3).Should().Be("ab…");

    [Fact]
    public void Excerpt_should_work_for_legacy_plain_text()
        => ArticleBodyContent.Excerpt("x\n\ny", ArticleBodyFormat.PlainText, 10).Should().Be("x y");
}
