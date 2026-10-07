using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

public class StoryTests
{
    [Fact]
    public void Create_should_reject_empty_title()
    {
        var act = () => Story.Create("", DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_start_in_PendingRelevanceReview()
    {
        var story = Story.Create("قصة", DateTimeOffset.UtcNow);
        story.Status.Should().Be(StoryStatus.PendingRelevanceReview);
    }

    [Theory]
    [InlineData(true, 0.9, StoryStatus.Relevant)]
    [InlineData(false, 0.9, StoryStatus.NotRelevant)]
    public void ApplyRelevanceVerdict_should_follow_the_engine_verdict_above_the_review_threshold(
        bool isRelevant, double confidence, StoryStatus expected)
    {
        var story = Story.Create("قصة", DateTimeOffset.UtcNow);

        story.ApplyRelevanceVerdict(isRelevant, (decimal)confidence, reviewThreshold: 0.6m, DateTimeOffset.UtcNow);

        story.Status.Should().Be(expected);
    }

    [Fact]
    public void ApplyRelevanceVerdict_should_route_to_human_review_when_confidence_is_below_threshold()
    {
        // A confidently-relevant verdict with low confidence must NOT
        // auto-publish — this is the "never auto-publish below threshold"
        // rule the engine spec requires (§13), and it must hold regardless
        // of what IsRelevant says.
        var story = Story.Create("قصة", DateTimeOffset.UtcNow);

        story.ApplyRelevanceVerdict(isRelevant: true, confidenceScore: 0.4m, reviewThreshold: 0.6m, DateTimeOffset.UtcNow);

        story.Status.Should().Be(StoryStatus.NeedsHumanReview);
    }

    [Fact]
    public void AttachArticle_should_update_LastUpdatedAtUtc()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var story = Story.Create("قصة", initialTime);
        var article = Article.Create(
            Guid.NewGuid(), "item-1", "https://example.com/x", "title", new string('a', 64),
            null, DateTimeOffset.UtcNow);

        var laterTime = initialTime.AddHours(1);
        story.AttachArticle(article, laterTime);

        story.LastUpdatedAtUtc.Should().Be(laterTime);
    }

    [Theory]
    [InlineData(true, StoryStatus.Relevant)]
    [InlineData(false, StoryStatus.NotRelevant)]
    public void ResolveHumanReview_should_set_status_from_a_NeedsHumanReview_story(bool isRelevant, StoryStatus expected)
    {
        var story = Story.Create("قصة", DateTimeOffset.UtcNow);
        story.ApplyRelevanceVerdict(isRelevant: true, confidenceScore: 0.4m, reviewThreshold: 0.6m, DateTimeOffset.UtcNow);
        story.Status.Should().Be(StoryStatus.NeedsHumanReview); // sanity-check the fixture

        var resolvedAt = DateTimeOffset.UtcNow.AddMinutes(5);
        story.ResolveHumanReview(isRelevant, resolvedAt);

        story.Status.Should().Be(expected);
        story.LastUpdatedAtUtc.Should().Be(resolvedAt);
    }

    [Theory]
    [InlineData(StoryStatus.PendingRelevanceReview)]
    [InlineData(StoryStatus.Relevant)]
    [InlineData(StoryStatus.NotRelevant)]
    public void ResolveHumanReview_should_reject_a_story_not_awaiting_review(StoryStatus nonReviewStatus)
    {
        var story = Story.Create("قصة", DateTimeOffset.UtcNow);
        if (nonReviewStatus != StoryStatus.PendingRelevanceReview)
        {
            // Route to a terminal status without going through
            // NeedsHumanReview — a high-confidence verdict either way.
            story.ApplyRelevanceVerdict(
                isRelevant: nonReviewStatus == StoryStatus.Relevant,
                confidenceScore: 0.95m, reviewThreshold: 0.6m, DateTimeOffset.UtcNow);
        }
        story.Status.Should().Be(nonReviewStatus); // sanity-check the fixture

        var act = () => story.ResolveHumanReview(true, DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }
}
