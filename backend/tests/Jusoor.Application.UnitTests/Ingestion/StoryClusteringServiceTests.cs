using FluentAssertions;
using Jusoor.Application.Ingestion;
using Jusoor.Application.UnitTests.TestSupport;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Application.UnitTests.Ingestion;

public class StoryClusteringServiceTests
{
    private static Source NewSource(string name = "Test Source") => new()
    {
        Name = name,
        Layer = SourceLayer.ProfessionalMedia,
        FeedUrl = "https://example.com/feed.xml"
    };

    [Fact]
    public async Task ClusterAsync_should_create_a_new_story_when_no_other_article_shares_its_content_hash()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var source = NewSource();
        context.Sources.Add(source);
        await context.SaveChangesAsync(default);

        var article = Article.Create(
            source.Id, "ext-1", "https://example.com/1", "Unique headline",
            new string('a', 64), null, DateTimeOffset.UtcNow);
        context.Articles.Add(article);

        var clusterer = new StoryClusteringService(context);
        var now = DateTimeOffset.UtcNow;

        var story = await clusterer.ClusterAsync(article, now, default);

        story.Should().NotBeNull();
        story.CanonicalTitle.Should().Be("Unique headline");
        article.StoryId.Should().Be(story.Id);
        story.Articles.Should().ContainSingle(a => a == article);
    }

    [Fact]
    public async Task ClusterAsync_should_attach_to_an_existing_story_when_another_already_clustered_article_shares_the_content_hash()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var sourceA = NewSource("Source A");
        var sourceB = NewSource("Source B");
        context.Sources.AddRange(sourceA, sourceB);

        var sharedHash = new string('b', 64);
        var now = DateTimeOffset.UtcNow;

        // Simulate a previous ingestion run: Source A's article already
        // exists AND has already been clustered (StoryId set) — this is
        // what a real prior IngestSourceCommandHandler run would have left
        // behind.
        var firstArticle = Article.Create(
            sourceA.Id, "ext-a1", "https://a.example.com/1", "Same wire story",
            sharedHash, null, now.AddMinutes(-10));
        var existingStory = Story.Create("Same wire story", now.AddMinutes(-10));
        firstArticle.AssignToStory(existingStory, now.AddMinutes(-10));
        context.Articles.Add(firstArticle);
        context.Stories.Add(existingStory);
        await context.SaveChangesAsync(default);

        // Source B now republishes byte-identical content under its own
        // external item id — this is the cross-source exact-duplicate case.
        var secondArticle = Article.Create(
            sourceB.Id, "ext-b1", "https://b.example.com/1", "Same wire story (B's headline)",
            sharedHash, null, now);
        context.Articles.Add(secondArticle);

        var clusterer = new StoryClusteringService(context);

        var story = await clusterer.ClusterAsync(secondArticle, now, default);

        story.Id.Should().Be(existingStory.Id);
        secondArticle.StoryId.Should().Be(existingStory.Id);
        story.Articles.Should().HaveCount(2);
        story.LastUpdatedAtUtc.Should().Be(now);
    }

    [Fact]
    public async Task ClusterAsync_should_not_match_an_article_that_shares_the_content_hash_but_was_never_itself_clustered()
    {
        // Known limitation, documented deliberately rather than silently:
        // an Article with a null StoryId (e.g. seeded/legacy data from
        // before this Slice existed) is not a valid clustering target —
        // matching against it would mean AssignToStory-ing onto an Article
        // instead of a real Story. Such rows are a backfill concern, not
        // something this stage should paper over.
        await using var context = TestApplicationDbContext.CreateNew();
        var sourceA = NewSource("Source A");
        var sourceB = NewSource("Source B");
        context.Sources.AddRange(sourceA, sourceB);

        var sharedHash = new string('c', 64);
        var now = DateTimeOffset.UtcNow;

        var unclusteredArticle = Article.Create(
            sourceA.Id, "ext-a1", "https://a.example.com/1", "Never clustered",
            sharedHash, null, now.AddMinutes(-10));
        context.Articles.Add(unclusteredArticle);
        await context.SaveChangesAsync(default);

        var newArticle = Article.Create(
            sourceB.Id, "ext-b1", "https://b.example.com/1", "Never clustered (B)",
            sharedHash, null, now);
        context.Articles.Add(newArticle);

        var clusterer = new StoryClusteringService(context);

        
	var story = await clusterer.ClusterAsync(newArticle, now, default);

	story.Should().NotBeNull();
	story.Articles.Should().ContainSingle(a => a == newArticle);
	newArticle.StoryId.Should().Be(story.Id);
	newArticle.StoryId.Should().NotBeNull();
	unclusteredArticle.StoryId.Should().BeNull();

    }
}
