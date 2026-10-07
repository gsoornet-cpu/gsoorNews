using FluentAssertions;
using Jusoor.Application.Relevance;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Application.UnitTests.Relevance;

public class RulesBasedRelevanceEngineTests
{
    private static Story StoryWithArticle(string title, string? content)
    {
        var now = DateTimeOffset.UtcNow;
        var article = Article.Create(
            Guid.NewGuid(), "ext-1", "https://example.com/1", title,
            new string('a', 64), null, now, content);
        var story = Story.Create(title, now);
        article.AssignToStory(story, now);
        return story;
    }

    [Fact]
    public async Task EvaluateAsync_should_return_high_confidence_relevant_for_a_diaspora_specific_phrase()
    {
        var story = StoryWithArticle(
            "Egyptian embassy statement",
            "The Egyptian community abroad reacted to the announcement.");

        var engine = new RulesBasedRelevanceEngine();
        var result = await engine.EvaluateAsync(story, default);

        result.IsRelevant.Should().BeTrue();
        result.ConfidenceScore.Should().Be(0.90m);
        result.Method.Should().Be(RelevanceMethod.RulesOnly);
        result.EngineVersion.Should().Be(RulesBasedRelevanceEngine.EngineVersion);
        result.Reasons.Should().Contain("egyptian embassy");
    }

    [Fact]
    public async Task EvaluateAsync_should_return_medium_confidence_relevant_for_a_bare_nationality_mention()
    {
        // Spec §07's own example: "an Egyptian who died/was arrested abroad"
        // needs nothing more than the nationality word itself.
        var story = StoryWithArticle(
            "Egyptian national arrested abroad",
            "Authorities confirmed the arrest took place last week.");

        var engine = new RulesBasedRelevanceEngine();
        var result = await engine.EvaluateAsync(story, default);

        result.IsRelevant.Should().BeTrue();
        result.ConfidenceScore.Should().Be(0.75m);
        result.Reasons.Should().Contain("egyptian");
    }

    [Fact]
    public async Task EvaluateAsync_should_return_low_confidence_for_a_bare_place_mention_with_no_nationality_signal()
    {
        var story = StoryWithArticle(
            "International summit held in Cairo",
            "Delegates from several countries met to discuss trade policy.");

        var engine = new RulesBasedRelevanceEngine();
        var result = await engine.EvaluateAsync(story, default);

        result.ConfidenceScore.Should().Be(0.50m);
        result.Reasons.Should().Contain("bare place mention");
    }

    [Fact]
    public async Task EvaluateAsync_should_return_high_confidence_not_relevant_when_no_signal_is_found_at_all()
    {
        var story = StoryWithArticle(
            "Stock markets close higher",
            "Major indices rose on positive earnings reports.");

        var engine = new RulesBasedRelevanceEngine();
        var result = await engine.EvaluateAsync(story, default);

        result.IsRelevant.Should().BeFalse();
        // High confidence — it's a confident "no", not an uncertain one, so
        // it must NOT fall below the review threshold and get routed to
        // human review by mistake.
        result.ConfidenceScore.Should().Be(0.90m);
    }

    [Fact]
    public async Task EvaluateAsync_should_prefer_a_strong_signal_over_a_weaker_one_when_both_are_present()
    {
        var story = StoryWithArticle(
            "Egyptian embassy in Cairo issues travel notice",
            "The notice concerns Egyptians abroad.");

        var engine = new RulesBasedRelevanceEngine();
        var result = await engine.EvaluateAsync(story, default);

        // Contains both a StrongSignals phrase ("egyptian embassy") and a
        // bare place mention ("cairo") — the strong signal must win.
        result.ConfidenceScore.Should().Be(0.90m);
    }

    [Fact]
    public async Task EvaluateAsync_should_consider_every_clustered_article_not_just_the_first()
    {
        var now = DateTimeOffset.UtcNow;
        var story = Story.Create("Neutral headline", now);

        var firstArticle = Article.Create(
            Guid.NewGuid(), "ext-1", "https://a.example.com/1", "Neutral headline",
            new string('a', 64), null, now, "No signal here.");
        firstArticle.AssignToStory(story, now);

        var secondArticle = Article.Create(
            Guid.NewGuid(), "ext-2", "https://b.example.com/1", "Different outlet's headline",
            new string('b', 64), null, now, "But this source mentions the Egyptian community abroad.");
        secondArticle.AssignToStory(story, now);

        var engine = new RulesBasedRelevanceEngine();
        var result = await engine.EvaluateAsync(story, default);

        result.IsRelevant.Should().BeTrue();
        result.ConfidenceScore.Should().Be(0.90m);
    }
}