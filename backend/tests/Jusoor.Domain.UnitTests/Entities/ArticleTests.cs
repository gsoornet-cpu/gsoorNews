using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

public class ArticleTests
{
    private static Article ValidArticle() => Article.Create(
        sourceId: Guid.NewGuid(),
        externalSourceItemId: "item-123",
        canonicalUrl: "https://example.com/news/item-123",
        title: "خبر تجريبي",
        contentHash: new string('a', 64),
        publishedAtUtc: DateTimeOffset.UtcNow,
        fetchedAtUtc: DateTimeOffset.UtcNow);

    [Fact]
    public void Create_should_succeed_for_valid_https_url()
    {
        var act = ValidArticle;
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/file")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    public void Create_should_reject_non_http_or_malformed_urls(string badUrl)
    {
        var act = () => Article.Create(
            Guid.NewGuid(), "item-1", badUrl, "title", new string('a', 64),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_reject_empty_external_id()
    {
        var act = () => Article.Create(
            Guid.NewGuid(), "", "https://example.com/x", "title", new string('a', 64),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("externalSourceItemId");
    }

    [Fact]
    public void AdvanceTo_should_move_status_forward()
    {
        var article = ValidArticle();
        article.AdvanceTo(ArticleProcessingStatus.Validated);
        article.Status.Should().Be(ArticleProcessingStatus.Validated);
    }

    [Fact]
    public void AdvanceTo_should_throw_once_article_is_in_a_terminal_state()
    {
        var article = ValidArticle();
        article.MarkFailed("fetch timeout");

        var act = () => article.AdvanceTo(ArticleProcessingStatus.Validated);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AssignToStory_should_link_both_sides_of_the_relationship()
    {
        var article = ValidArticle();
        var story = Story.Create("قصة تجريبية", DateTimeOffset.UtcNow);

        article.AssignToStory(story, DateTimeOffset.UtcNow);

        article.StoryId.Should().Be(story.Id);
        story.Articles.Should().Contain(article);
    }
}
