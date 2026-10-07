using FluentAssertions;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Review;
using Jusoor.Application.UnitTests.TestSupport;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jusoor.Application.UnitTests.Review;

public class ReviewQueueTests
{
    private static Source NewSource(string name) => new()
    {
        Name = name,
        Layer = SourceLayer.ProfessionalMedia,
        FeedUrl = "https://example.com/feed.xml"
    };

    private static IDateTimeProvider FixedClock(DateTimeOffset now)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    private static Story NewStoryInReview(string title, DateTimeOffset firstSeenAt)
    {
        var story = Story.Create(title, firstSeenAt);
        story.ApplyRelevanceVerdict(isRelevant: true, confidenceScore: 0.4m, reviewThreshold: 0.6m, firstSeenAt);
        return story;
    }

    [Fact]
    public async Task GetReviewQueueQuery_should_only_return_stories_needing_human_review()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var now = DateTimeOffset.UtcNow;
        var source = NewSource("Source A");
        context.Sources.Add(source);

        var pending = NewStoryInReview("Ambiguous story", now);
        var relevant = Story.Create("Clear story", now);
        relevant.ApplyRelevanceVerdict(isRelevant: true, confidenceScore: 0.95m, reviewThreshold: 0.6m, now);

        foreach (var (story, suffix) in new[] { (pending, "1"), (relevant, "2") })
        {
            var article = Article.Create(source.Id, $"ext-{suffix}", $"https://example.com/{suffix}",
                story.CanonicalTitle, new string('a', 64), now, now, "content");
            article.AssignToStory(story, now);
            context.Stories.Add(story);
        }

        await context.SaveChangesAsync(default);

        var handler = new GetReviewQueueQueryHandler(context);
        var result = await handler.Handle(new GetReviewQueueQuery(), default);

        result.Items.Should().ContainSingle(i => i.Title == "Ambiguous story");
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetReviewQueueQuery_should_order_oldest_first()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var now = DateTimeOffset.UtcNow;
        var source = NewSource("Source A");
        context.Sources.Add(source);

        for (var i = 0; i < 3; i++)
        {
            var firstSeenAt = now.AddMinutes(-3 + i); // item 0 is oldest
            var story = NewStoryInReview($"Story {i}", firstSeenAt);
            var article = Article.Create(source.Id, $"ext-{i}", $"https://example.com/{i}",
                story.CanonicalTitle, new string('a', 64), firstSeenAt, firstSeenAt, "content");
            article.AssignToStory(story, firstSeenAt);
            context.Stories.Add(story);
        }

        await context.SaveChangesAsync(default);

        var handler = new GetReviewQueueQueryHandler(context);
        var result = await handler.Handle(new GetReviewQueueQuery(), default);

        result.Items.Select(i => i.Title).Should().ContainInOrder("Story 0", "Story 1", "Story 2");
    }

    [Fact]
    public async Task GetReviewQueueQuery_should_surface_the_latest_relevance_evaluation_context()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var now = DateTimeOffset.UtcNow;
        var source = NewSource("Source A");
        var story = NewStoryInReview("Ambiguous story", now);
        var article = Article.Create(source.Id, "ext-1", "https://example.com/1",
            story.CanonicalTitle, new string('a', 64), now, now, "content");
        article.AssignToStory(story, now);

        context.Sources.Add(source);
        context.Stories.Add(story);
        context.RelevanceResults.Add(RelevanceResult.Create(
            story.Id, article.Id, true, 0.40m, RelevanceMethod.RulesOnly, "bare place mention only", "rules-v1", now));
        await context.SaveChangesAsync(default);

        var handler = new GetReviewQueueQueryHandler(context);
        var result = await handler.Handle(new GetReviewQueueQuery(), default);

        var item = result.Items.Should().ContainSingle().Subject;
        item.LatestConfidenceScore.Should().Be(0.40m);
        item.LatestReasons.Should().Be("bare place mention only");
    }

    [Fact]
    public async Task GetReviewQueueItemQuery_should_return_null_for_a_story_not_awaiting_review()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var now = DateTimeOffset.UtcNow;
        var relevant = Story.Create("Clear story", now);
        relevant.ApplyRelevanceVerdict(isRelevant: true, confidenceScore: 0.95m, reviewThreshold: 0.6m, now);
        context.Stories.Add(relevant);
        await context.SaveChangesAsync(default);

        var handler = new GetReviewQueueItemQueryHandler(context);

        (await handler.Handle(new GetReviewQueueItemQuery(relevant.Id), default)).Should().BeNull();
        (await handler.Handle(new GetReviewQueueItemQuery(Guid.NewGuid()), default)).Should().BeNull();
    }

    [Fact]
    public async Task GetReviewQueueItemQuery_should_return_full_detail_including_every_article_and_relevance_history()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var now = DateTimeOffset.UtcNow;
        var sourceA = NewSource("Outlet A");
        var sourceB = NewSource("Outlet B");
        var story = NewStoryInReview("Ambiguous story", now);
        var articleA = Article.Create(sourceA.Id, "ext-a", "https://a.example.com/1", story.CanonicalTitle, new string('a', 64), now, now, "content A");
        var articleB = Article.Create(sourceB.Id, "ext-b", "https://b.example.com/1", story.CanonicalTitle, new string('b', 64), now, now, "content B");
        articleA.AssignToStory(story, now);
        articleB.AssignToStory(story, now);

        context.Sources.AddRange(sourceA, sourceB);
        context.Stories.Add(story);
        context.RelevanceResults.Add(RelevanceResult.Create(story.Id, articleA.Id, true, 0.4m, RelevanceMethod.RulesOnly, "reason 1", "rules-v1", now));
        await context.SaveChangesAsync(default);

        var handler = new GetReviewQueueItemQueryHandler(context);
        var result = await handler.Handle(new GetReviewQueueItemQuery(story.Id), default);

        result.Should().NotBeNull();
        result!.Articles.Should().HaveCount(2);
        result.Articles.Select(a => a.SourceName).Should().Contain(["Outlet A", "Outlet B"]);
        result.RelevanceHistory.Should().ContainSingle(r => r.Reasons == "reason 1");
    }

    [Fact]
    public async Task ResolveReviewCommand_should_resolve_a_pending_story_and_record_the_decision()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var now = DateTimeOffset.UtcNow;
        var story = NewStoryInReview("Ambiguous story", now);
        context.Stories.Add(story);
        await context.SaveChangesAsync(default);

        var resolvedAt = now.AddMinutes(10);
        var handler = new ResolveReviewCommandHandler(context, FixedClock(resolvedAt), NullLogger<ResolveReviewCommandHandler>.Instance);

        var result = await handler.Handle(
            new ResolveReviewCommand(story.Id, true, "Clearly about an Egyptian expatriate abroad", "editor-1"), default);

        result.Succeeded.Should().BeTrue();
        result.Outcome.Should().Be(ResolveReviewOutcome.Resolved);
        result.NewStatus.Should().Be(StoryStatus.Relevant);

        context.Stories.Single().Status.Should().Be(StoryStatus.Relevant);
        var decision = context.HumanReviewDecisions.Single();
        decision.ReviewedByUserId.Should().Be("editor-1");
        decision.IsRelevant.Should().BeTrue();
        decision.Reasoning.Should().Be("Clearly about an Egyptian expatriate abroad");
        decision.DecidedAtUtc.Should().Be(resolvedAt);
    }

    [Fact]
    public async Task ResolveReviewCommand_should_return_StoryNotFound_for_a_missing_story()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var handler = new ResolveReviewCommandHandler(context, FixedClock(DateTimeOffset.UtcNow), NullLogger<ResolveReviewCommandHandler>.Instance);

        var result = await handler.Handle(
            new ResolveReviewCommand(Guid.NewGuid(), true, "some reasoning here", "editor-1"), default);

        result.Succeeded.Should().BeFalse();
        result.Outcome.Should().Be(ResolveReviewOutcome.StoryNotFound);
    }

    [Fact]
    public async Task ResolveReviewCommand_should_return_StoryNotInReview_for_an_already_resolved_story()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var now = DateTimeOffset.UtcNow;
        var story = Story.Create("Already decided", now);
        story.ApplyRelevanceVerdict(isRelevant: true, confidenceScore: 0.95m, reviewThreshold: 0.6m, now);
        context.Stories.Add(story);
        await context.SaveChangesAsync(default);

        var handler = new ResolveReviewCommandHandler(context, FixedClock(now), NullLogger<ResolveReviewCommandHandler>.Instance);
        var result = await handler.Handle(
            new ResolveReviewCommand(story.Id, false, "some reasoning here", "editor-1"), default);

        result.Succeeded.Should().BeFalse();
        result.Outcome.Should().Be(ResolveReviewOutcome.StoryNotInReview);
        result.NewStatus.Should().Be(StoryStatus.Relevant); // reports the actual current status
    }

    [Theory]
    [InlineData("")]
    [InlineData("too short")]
    public void ResolveReviewCommandValidator_should_reject_missing_or_too_short_reasoning(string reasoning)
    {
        var validator = new ResolveReviewCommandValidator();

        var result = validator.Validate(new ResolveReviewCommand(Guid.NewGuid(), true, reasoning, "editor-1"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ResolveReviewCommandValidator_should_accept_a_properly_explained_decision()
    {
        var validator = new ResolveReviewCommandValidator();

        var result = validator.Validate(new ResolveReviewCommand(
            Guid.NewGuid(), true, "Clearly about an Egyptian expatriate abroad, confirmed via consulate mention", "editor-1"));

        result.IsValid.Should().BeTrue();
    }
}
