using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

/// <summary>Slice 21 (decision D2): the body is HTML now; rows from before the slice are plain text.</summary>
public class EditorialArticleBodyFormatTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A row as it exists after the migration back-fills older articles with 0.</summary>
    private static EditorialArticle LegacyPlainTextArticle()
    {
        var article = EditorialArticle.Create("عنوان", null, "نص قديم", "rep-1", null, null);
        typeof(EditorialArticle).GetProperty(nameof(EditorialArticle.BodyFormat))!
            .SetValue(article, ArticleBodyFormat.PlainText);
        return article;
    }

    [Fact]
    public void Body_format_values_should_be_pinned_because_they_are_persisted_as_ints()
    {
        // 0 is PlainText on purpose: the migration back-fills existing rows with the column's
        // zero default, so legacy rows are PlainText without a hand-edited default value.
        ((int)ArticleBodyFormat.PlainText).Should().Be(0);
        ((int)ArticleBodyFormat.Html).Should().Be(1);
    }

    [Fact]
    public void Create_should_mark_the_body_as_html()
    {
        var article = EditorialArticle.Create("عنوان", null, "<p>نص</p>", "rep-1", null, null);

        article.BodyFormat.Should().Be(ArticleBodyFormat.Html);
    }

    [Fact]
    public void UpdateContent_should_turn_a_legacy_plain_text_article_into_html()
    {
        var article = LegacyPlainTextArticle();
        article.BodyFormat.Should().Be(ArticleBodyFormat.PlainText);

        article.UpdateContent("عنوان", null, "<p>نص جديد</p>", null, null);

        article.BodyFormat.Should().Be(ArticleBodyFormat.Html);
        article.Body.Should().Be("<p>نص جديد</p>");
    }

    [Fact]
    public void A_revision_should_keep_the_format_of_the_text_it_replaced()
    {
        var legacy = LegacyPlainTextArticle();
        legacy.Publish("pub-1", Now.AddHours(-1));
        var modern = EditorialArticle.Create("عنوان", null, "<p>نص</p>", "rep-1", null, null);
        modern.Publish("pub-1", Now.AddHours(-1));

        var fromLegacy = EditorialArticleRevision.CaptureBeforeEdit(legacy, 1, "senior-1", new[] { NewsroomRole.SeniorEditor }, null, Now);
        var fromModern = EditorialArticleRevision.CaptureBeforeEdit(modern, 1, "senior-1", new[] { NewsroomRole.SeniorEditor }, null, Now);

        fromLegacy.BodyFormat.Should().Be(ArticleBodyFormat.PlainText);
        fromLegacy.Body.Should().Be("نص قديم");
        fromModern.BodyFormat.Should().Be(ArticleBodyFormat.Html);
    }
}
