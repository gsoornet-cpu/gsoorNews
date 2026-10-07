using FluentAssertions;
using Jusoor.Application.Stories;
using Jusoor.Application.UnitTests.TestSupport;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Application.UnitTests.Stories;

public class StoryQueriesTests
{
    private static Source NewSource(string name) => new()
    {
        Name = name,
        Layer = SourceLayer.ProfessionalMedia,
        FeedUrl = "https://example.com/feed.xml"
    };

    private static Story NewStory(string title, StoryStatus status, DateTimeOffset now)
    {
        var story = Story.Create(title, now);
        // Status starts at PendingRelevanceReview from Create(); route
        // through the same real transition Slice 5 uses rather than
        // reflecting the private setter, so these tests exercise the same
        // path production code does.
        story.ApplyRelevanceVerdict(
            isRelevant: status == StoryStatus.Relevant,
            confidenceScore: status == StoryStatus.NeedsHumanReview ? 0.1m : 0.9m,
            reviewThreshold: 0.6m,
            nowUtc: now);
        return story;
    }

    [Fact]
    public async Task GetHomepageStoriesQuery_should_only_return_relevant_stories()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var now = DateTimeOffset.UtcNow;
        var source = NewSource("Source A");
        context.Sources.Add(source);

        var relevant = NewStory("Relevant story", StoryStatus.Relevant, now);
        var pending = NewStory("Pending story", StoryStatus.NeedsHumanReview, now);
        var notRelevant = NewStory("Irrelevant story", StoryStatus.NotRelevant, now);

        foreach (var (story, suffix) in new[] { (relevant, "1"), (pending, "2"), (notRelevant, "3") })
        {
            var article = Article.Create(source.Id, $"ext-{suffix}", $"https://example.com/{suffix}",
                story.CanonicalTitle, new string('a', 64), now, now, "content");
            article.AssignToStory(story, now);
            context.Stories.Add(story);
        }

        await context.SaveChangesAsync(default);

        var handler = new GetHomepageStoriesQueryHandler(context);
        var result = await handler.Handle(new GetHomepageStoriesQuery(), default);

        result.Items.Should().ContainSingle(s => s.Title == "Relevant story");
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetHomepageStoriesQuery_should_paginate_and_order_by_most_recently_updated()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var now = DateTimeOffset.UtcNow;
        var source = NewSource("Source A");
        context.Sources.Add(source);

        for (var i = 0; i < 3; i++)
        {
            var updatedAt = now.AddMinutes(i); // story 2 is most recently updated
            var story = NewStory($"Story {i}", StoryStatus.Relevant, updatedAt);
            var article = Article.Create(source.Id, $"ext-{i}", $"https://example.com/{i}",
                story.CanonicalTitle, new string('a', 64), updatedAt, updatedAt, "content");
            article.AssignToStory(story, updatedAt);
            context.Stories.Add(story);
        }

        await context.SaveChangesAsync(default);

        var handler = new GetHomepageStoriesQueryHandler(context);
        var page1 = await handler.Handle(new GetHomepageStoriesQuery(Page: 1, PageSize: 2), default);
        var page2 = await handler.Handle(new GetHomepageStoriesQuery(Page: 2, PageSize: 2), default);

        page1.Items.Should().HaveCount(2);
        page1.Items[0].Title.Should().Be("Story 2"); // most recently updated first
        page1.TotalCount.Should().Be(3);
        page2.Items.Should().ContainSingle(s => s.Title == "Story 0");
    }

    [Fact]
    public async Task GetHomepageStoriesQuery_should_include_geography_and_truncated_excerpt_when_present()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var now = DateTimeOffset.UtcNow;
        var source = NewSource("Source A");
        var uae = new Country { NameAr = "الإمارات", NameEn = "United Arab Emirates", IsoCode2 = "AE", Slug = "uae" };
        var dubai = new City { CountryId = uae.Id, Country = uae, NameAr = "دبي", NameEn = "Dubai", Slug = "dubai" };

        var story = NewStory("Long story", StoryStatus.Relevant, now);
        story.SetGeography(uae.Id, dubai.Id);
        var longContent = new string('س', 300);
        var article = Article.Create(source.Id, "ext-1", "https://example.com/1",
            story.CanonicalTitle, new string('a', 64), now, now, longContent);
        article.AssignToStory(story, now);

        context.Sources.Add(source);
	context.Countries.Add(uae);
	context.Cities.Add(dubai);
	context.Stories.Add(story);

	await context.SaveChangesAsync(default);

        var handler = new GetHomepageStoriesQueryHandler(context);
        var result = await handler.Handle(new GetHomepageStoriesQuery(), default);

        var item = result.Items.Should().ContainSingle().Subject;
        item.CountryNameEn.Should().Be("United Arab Emirates");
        item.CityNameEn.Should().Be("Dubai");
        item.Excerpt.Should().HaveLength(221).And.EndWith("…"); // 220 chars + ellipsis
        item.SourceNames.Should().ContainSingle("Source A");
    }

    [Fact]
    public async Task GetStoryByIdQuery_should_return_null_for_non_relevant_or_missing_story()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var now = DateTimeOffset.UtcNow;
        var source = NewSource("Source A");
        var pending = NewStory("Pending", StoryStatus.NeedsHumanReview, now);
        var article = Article.Create(source.Id, "ext-1", "https://example.com/1",
            pending.CanonicalTitle, new string('a', 64), now, now, "content");
        article.AssignToStory(pending, now);

        context.Sources.Add(source);
        context.Stories.Add(pending);
        await context.SaveChangesAsync(default);

        var handler = new GetStoryByIdQueryHandler(context);

        (await handler.Handle(new GetStoryByIdQuery(pending.Id), default)).Should().BeNull();
        (await handler.Handle(new GetStoryByIdQuery(Guid.NewGuid()), default)).Should().BeNull();
    }

    [Fact]
    public async Task GetStoryByIdQuery_should_render_the_longest_article_and_list_every_source()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var now = DateTimeOffset.UtcNow;
        var sourceA = NewSource("Short Outlet");
        var sourceB = NewSource("Long Outlet");

        var story = NewStory("Multi-source story", StoryStatus.Relevant, now);
        var shortArticle = Article.Create(sourceA.Id, "ext-a", "https://a.example.com/1",
            story.CanonicalTitle, new string('a', 64), now, now, "short");
        var longArticle = Article.Create(sourceB.Id, "ext-b", "https://b.example.com/1",
            story.CanonicalTitle, new string('b', 64), now, now, "this is the much longer article body");
        shortArticle.AssignToStory(story, now);
        longArticle.AssignToStory(story, now);

        context.Sources.AddRange(sourceA, sourceB);
        context.Stories.Add(story);
        await context.SaveChangesAsync(default);

        var handler = new GetStoryByIdQueryHandler(context);
        var result = await handler.Handle(new GetStoryByIdQuery(story.Id), default);

        result.Should().NotBeNull();
        result!.Content.Should().Be("this is the much longer article body");
        result.Sources.Should().HaveCount(2);
        result.Sources.Select(s => s.SourceName).Should().Contain(["Short Outlet", "Long Outlet"]);
    }
}
