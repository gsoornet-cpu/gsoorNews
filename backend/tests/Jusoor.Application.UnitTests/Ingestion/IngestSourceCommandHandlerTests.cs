using FluentAssertions;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Geography;
using Jusoor.Application.Ingestion;
using Jusoor.Application.Ingestion.Contracts;
using Jusoor.Application.Relevance;
using Jusoor.Application.UnitTests.TestSupport;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Jusoor.Application.UnitTests.Ingestion;

public class IngestSourceCommandHandlerTests
{
    private static Source NewSource() => new()
    {
        Name = "Test Source",
        Layer = SourceLayer.ProfessionalMedia,
        FeedUrl = "https://example.com/feed.xml"
    };

    private static IngestSourceCommandHandler CreateHandler(
        TestApplicationDbContext context, ISourceContentFetcher fetcher, IFeedItemParser parser, IDateTimeProvider clock,
        decimal reviewThreshold = 0.6m)
    {
        // The real StoryClusteringService, RulesBasedRelevanceEngine, and
        // RulesBasedGeographicExtractor, not substitutes — all three are
        // pure deterministic logic (no external I/O), so exercising them
        // for real against the same in-memory context is both simpler and
        // more representative than mocking them, matching how these tests
        // already use a real (InMemory) DbContext rather than mocking data
        // access.
        var storyClusterer = new StoryClusteringService(context);
        var relevanceEngine = new RulesBasedRelevanceEngine();
        var relevanceOptions = Options.Create(new RelevanceOptions { ReviewThreshold = reviewThreshold });
        var geographicExtractor = new RulesBasedGeographicExtractor();
        return new IngestSourceCommandHandler(
            context, fetcher, parser, storyClusterer, relevanceEngine, relevanceOptions, geographicExtractor, clock,
            NullLogger<IngestSourceCommandHandler>.Instance);
    }

    private static IDateTimeProvider FixedClock(DateTimeOffset now)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    [Fact]
    public async Task Handle_should_ingest_all_new_items_from_a_valid_feed()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var source = NewSource();
        context.Sources.Add(source);
        await context.SaveChangesAsync(default);

        var fetcher = Substitute.For<ISourceContentFetcher>();
        fetcher.FetchAsync(source.FeedUrl!, Arg.Any<CancellationToken>())
            .Returns(SourceFetchResult.Success("<rss/>", 200));

        var parser = Substitute.For<IFeedItemParser>();
        parser.Parse(Arg.Any<string>()).Returns(new List<FeedItemDto>
        {
            new("ext-1", "Title 1", "https://example.com/1", "content 1", DateTimeOffset.UtcNow),
            new("ext-2", "Title 2", "https://example.com/2", "content 2", DateTimeOffset.UtcNow),
        });

        var handler = CreateHandler(context, fetcher, parser, FixedClock(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new IngestSourceCommand(source.Id), default);

        result.Succeeded.Should().BeTrue();
        result.ItemsFound.Should().Be(2);
        result.ItemsIngested.Should().Be(2);
        result.ItemsSkippedAsDuplicate.Should().Be(0);
        context.Articles.Should().HaveCount(2);

        // Relevance Engine v1 (Slice 5) wiring: every ingested article gets
        // a RelevanceResult and its Story gets a verdict — "content 1"/
        // "content 2" carry no Egyptian-diaspora signal, so both should
        // confidently land on NotRelevant, not NeedsHumanReview.
        context.RelevanceResults.Should().HaveCount(2);
        context.Stories.Should().OnlyContain(s => s.Status == StoryStatus.NotRelevant);
    }

    [Fact]
    public async Task Handle_should_skip_items_already_ingested_for_this_source()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var source = NewSource();
        context.Sources.Add(source);

        var existingArticle = Article.Create(
            source.Id, "ext-1", "https://example.com/1", "Title 1", new string('a', 64), null, DateTimeOffset.UtcNow);
        context.Articles.Add(existingArticle);
        await context.SaveChangesAsync(default);

        var fetcher = Substitute.For<ISourceContentFetcher>();
        fetcher.FetchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SourceFetchResult.Success("<rss/>", 200));

        var parser = Substitute.For<IFeedItemParser>();
        parser.Parse(Arg.Any<string>()).Returns(new List<FeedItemDto>
        {
            new("ext-1", "Title 1", "https://example.com/1", "content 1", null), // already known
            new("ext-2", "Title 2", "https://example.com/2", "content 2", null), // new
        });

        var handler = CreateHandler(context, fetcher, parser, FixedClock(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new IngestSourceCommand(source.Id), default);

        result.ItemsIngested.Should().Be(1);
        result.ItemsSkippedAsDuplicate.Should().Be(1);
        context.Articles.Should().HaveCount(2); // the 1 pre-existing + the 1 newly ingested
    }

    [Fact]
    public async Task Handle_should_skip_one_malformed_item_without_failing_the_whole_batch()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var source = NewSource();
        context.Sources.Add(source);
        await context.SaveChangesAsync(default);

        var fetcher = Substitute.For<ISourceContentFetcher>();
        fetcher.FetchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SourceFetchResult.Success("<rss/>", 200));

        var parser = Substitute.For<IFeedItemParser>();
        parser.Parse(Arg.Any<string>()).Returns(new List<FeedItemDto>
        {
            new("ext-1", "Good item", "https://example.com/1", "content", null),
            new("ext-2", "Bad item", "not-a-valid-url", "content", null), // Article.Create will reject this
        });

        var handler = CreateHandler(context, fetcher, parser, FixedClock(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new IngestSourceCommand(source.Id), default);

        result.Succeeded.Should().BeTrue();
        result.ItemsIngested.Should().Be(1);
        context.Articles.Should().ContainSingle(a => a.ExternalSourceItemId == "ext-1");
    }

    [Fact]
    public async Task Handle_should_return_failure_and_log_when_fetch_fails()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var source = NewSource();
        context.Sources.Add(source);
        await context.SaveChangesAsync(default);

        var fetcher = Substitute.For<ISourceContentFetcher>();
        fetcher.FetchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SourceFetchResult.Failure(SourceFetchOutcome.Timeout, "timed out"));

        var parser = Substitute.For<IFeedItemParser>();

        var handler = CreateHandler(context, fetcher, parser, FixedClock(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new IngestSourceCommand(source.Id), default);

        result.Succeeded.Should().BeFalse();
        result.Outcome.Should().Be(SourceFetchOutcome.Timeout);
        context.SourceFetchLogs.Should().ContainSingle(l => l.Outcome == SourceFetchOutcome.Timeout);
        parser.DidNotReceiveWithAnyArgs().Parse(default!);
    }

    [Fact]
    public async Task Handle_should_return_failure_when_feed_content_is_malformed()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var source = NewSource();
        context.Sources.Add(source);
        await context.SaveChangesAsync(default);

        var fetcher = Substitute.For<ISourceContentFetcher>();
        fetcher.FetchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SourceFetchResult.Success("not xml", 200));

        var parser = Substitute.For<IFeedItemParser>();
        parser.Parse(Arg.Any<string>()).Returns(x => throw new FeedParseException("bad xml"));

        var handler = CreateHandler(context, fetcher, parser, FixedClock(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new IngestSourceCommand(source.Id), default);

        result.Succeeded.Should().BeFalse();
        result.Outcome.Should().Be(SourceFetchOutcome.ParseError);
    }

    [Fact]
    public async Task Handle_should_skip_inactive_sources_without_calling_the_fetcher()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var source = NewSource();
        source.IsActive = false;
        context.Sources.Add(source);
        await context.SaveChangesAsync(default);

        var fetcher = Substitute.For<ISourceContentFetcher>();
        var parser = Substitute.For<IFeedItemParser>();

        var handler = CreateHandler(context, fetcher, parser, FixedClock(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new IngestSourceCommand(source.Id), default);

        result.Succeeded.Should().BeTrue();
        result.ItemsFound.Should().Be(0);
        await fetcher.DidNotReceiveWithAnyArgs().FetchAsync(default!, default);
    }

    [Fact]
    public async Task Handle_should_mark_story_relevant_when_content_has_a_strong_diaspora_signal()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var source = NewSource();
        context.Sources.Add(source);
        await context.SaveChangesAsync(default);

        var fetcher = Substitute.For<ISourceContentFetcher>();
        fetcher.FetchAsync(source.FeedUrl!, Arg.Any<CancellationToken>())
            .Returns(SourceFetchResult.Success("<rss/>", 200));

        var parser = Substitute.For<IFeedItemParser>();
        parser.Parse(Arg.Any<string>()).Returns(new List<FeedItemDto>
        {
            new("ext-1", "Egyptian embassy statement", "https://example.com/1",
                "The Egyptian embassy issued a statement about the Egyptian community abroad.", null),
        });

        var handler = CreateHandler(context, fetcher, parser, FixedClock(DateTimeOffset.UtcNow));

        await handler.Handle(new IngestSourceCommand(source.Id), default);

        context.RelevanceResults.Should().ContainSingle(r => r.IsRelevant && r.Method == RelevanceMethod.RulesOnly);
        context.Stories.Should().ContainSingle(s => s.Status == StoryStatus.Relevant);
    }

    [Fact]
    public async Task Handle_should_route_ambiguous_content_to_human_review_instead_of_guessing()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var source = NewSource();
        context.Sources.Add(source);
        await context.SaveChangesAsync(default);

        var fetcher = Substitute.For<ISourceContentFetcher>();
        fetcher.FetchAsync(source.FeedUrl!, Arg.Any<CancellationToken>())
            .Returns(SourceFetchResult.Success("<rss/>", 200));

        var parser = Substitute.For<IFeedItemParser>();
        parser.Parse(Arg.Any<string>()).Returns(new List<FeedItemDto>
        {
            // Mentions Egypt only as a bare place name — no nationality or
            // diaspora-specific signal alongside it. Per RulesBasedRelevanceEngine
            // this is the "ambiguous" tier (confidence 0.50), which the
            // default 0.6 threshold routes to NeedsHumanReview.
            new("ext-1", "International summit held in Cairo", "https://example.com/1",
                "Delegates from several countries met to discuss trade policy.", null),
        });

        var handler = CreateHandler(context, fetcher, parser, FixedClock(DateTimeOffset.UtcNow));

        await handler.Handle(new IngestSourceCommand(source.Id), default);

        context.Stories.Should().ContainSingle(s => s.Status == StoryStatus.NeedsHumanReview);
    }

    [Fact]
    public async Task Handle_should_set_story_geography_when_a_known_city_is_mentioned()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var source = NewSource();

        var uae = new Country { NameAr = "الإمارات", NameEn = "United Arab Emirates", IsoCode2 = "AE", Slug = "uae" };
        var dubai = new City { CountryId = uae.Id, Country = uae, NameAr = "دبي", NameEn = "Dubai", Slug = "dubai" };
        uae.Cities.Add(dubai);

        context.Sources.Add(source);
        context.Countries.Add(uae);
        await context.SaveChangesAsync(default);

        var fetcher = Substitute.For<ISourceContentFetcher>();
        fetcher.FetchAsync(source.FeedUrl!, Arg.Any<CancellationToken>())
            .Returns(SourceFetchResult.Success("<rss/>", 200));

        var parser = Substitute.For<IFeedItemParser>();
        parser.Parse(Arg.Any<string>()).Returns(new List<FeedItemDto>
        {
            new("ext-1", "Egyptian man arrested in Dubai", "https://example.com/1",
                "An Egyptian expatriate was arrested by Dubai police, the Egyptian consulate confirmed.", null),
        });

        var handler = CreateHandler(context, fetcher, parser, FixedClock(DateTimeOffset.UtcNow));

        await handler.Handle(new IngestSourceCommand(source.Id), default);

        // Dubai (the specific, more informative host-city signal) must win
        // over the merely-repeated "Egyptian" nationality signal — see
        // RulesBasedGeographicExtractor's own doc comment for why.
        context.Stories.Should().ContainSingle(s => s.PrimaryCityId == dubai.Id && s.PrimaryCountryId == uae.Id);
    }

    [Fact]
    public async Task Handle_should_leave_story_geography_null_when_no_known_place_matches()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var source = NewSource();
        context.Sources.Add(source);
        await context.SaveChangesAsync(default);
        // Deliberately no Country/City rows in the gazetteer for this test.

        var fetcher = Substitute.For<ISourceContentFetcher>();
        fetcher.FetchAsync(source.FeedUrl!, Arg.Any<CancellationToken>())
            .Returns(SourceFetchResult.Success("<rss/>", 200));

        var parser = Substitute.For<IFeedItemParser>();
        parser.Parse(Arg.Any<string>()).Returns(new List<FeedItemDto>
        {
            new("ext-1", "Title 1", "https://example.com/1", "content 1", null),
        });

        var handler = CreateHandler(context, fetcher, parser, FixedClock(DateTimeOffset.UtcNow));

        await handler.Handle(new IngestSourceCommand(source.Id), default);

        context.Stories.Should().ContainSingle(s => s.PrimaryCountryId == null && s.PrimaryCityId == null);
    }
}