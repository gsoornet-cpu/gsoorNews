using FluentAssertions;
using Jusoor.Domain.Entities;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

public sealed class EditorialArticleSeoTests
{
    private static EditorialArticle Create() => EditorialArticle.Create("عنوان عربي للمقال", "ملخص الخبر", "النص", "editor", null, null);

    [Fact]
    public void New_articles_get_a_localized_url_safe_unique_slug()
    {
        var first = Create();
        var second = Create();
        first.Slug.Should().MatchRegex("^[\\p{L}\\p{Nd}-]+$");
        first.Slug.Should().NotBe(second.Slug);
    }

    [Fact]
    public void Seo_metadata_is_trimmed_and_slug_is_normalized()
    {
        var article = Create();
        article.UpdateSeo(new string('t', 50), new string('d', 150), "  خبر جديد  ", null,
            "عنوان مشاركة", null, null, false, false);
        article.Slug.Should().Be("خبر-جديد");
        article.SeoTitle.Should().HaveLength(50);
    }

    [Theory]
    [InlineData(49)]
    [InlineData(61)]
    public void Seo_title_must_be_50_to_60_characters(int length)
    {
        var act = () => Create().UpdateSeo(new string('t', length), null, "slug", null, null, null, null, false, false);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Non_http_canonical_is_rejected()
    {
        var act = () => Create().UpdateSeo(null, null, "slug", "javascript:alert(1)", null, null, null, false, false);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Secondary_tags_are_trimmed_deduplicated_and_bounded()
    {
        var article = Create();
        article.UpdateTaxonomy(null, [" تعليم ", "تعليم", "الأسرة"]);
        article.SecondaryTags.Should().Equal("تعليم", "الأسرة");
        var act = () => article.UpdateTaxonomy(null, Enumerable.Range(0, 13).Select(x => $"وسم{x}"));
        act.Should().Throw<ArgumentException>();
    }
}
