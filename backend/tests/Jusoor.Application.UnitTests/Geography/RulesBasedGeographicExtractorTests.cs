using FluentAssertions;
using Jusoor.Application.Geography;
using Jusoor.Domain.Entities;
using Xunit;

namespace Jusoor.Application.UnitTests.Geography;

public class RulesBasedGeographicExtractorTests
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

    private static Country CountryWithCities(string nameAr, string nameEn, string isoCode2, string slug, params City[] cities)
    {
        var country = new Country { NameAr = nameAr, NameEn = nameEn, IsoCode2 = isoCode2, Slug = slug };
        foreach (var city in cities)
        {
            city.CountryId = country.Id;
            city.Country = country;
            country.Cities.Add(city);
        }

        return country;
    }

    private static City NewCity(string nameAr, string nameEn, string slug) =>
        new() { CountryId = Guid.Empty, NameAr = nameAr, NameEn = nameEn, Slug = slug };

    [Fact]
    public async Task ExtractAsync_should_return_null_geography_when_gazetteer_is_empty()
    {
        var story = StoryWithArticle("Egyptian arrested in Dubai", "content");
        var extractor = new RulesBasedGeographicExtractor();

        var result = await extractor.ExtractAsync(story, [], default);

        result.CountryId.Should().BeNull();
        result.CityId.Should().BeNull();
        result.ExtractorVersion.Should().Be(RulesBasedGeographicExtractor.ExtractorVersion);
    }

    [Fact]
    public async Task ExtractAsync_should_return_null_geography_when_no_known_place_matches()
    {
        var story = StoryWithArticle("Stock markets close higher", "Major indices rose on positive earnings reports.");
        var uae = CountryWithCities("الإمارات", "United Arab Emirates", "AE", "uae", NewCity("دبي", "Dubai", "dubai"));
        var extractor = new RulesBasedGeographicExtractor();

        var result = await extractor.ExtractAsync(story, [uae], default);

        result.CountryId.Should().BeNull();
        result.CityId.Should().BeNull();
    }

    [Fact]
    public async Task ExtractAsync_should_match_a_bare_country_mention_when_no_city_is_named()
    {
        var story = StoryWithArticle("Egyptian community abroad", "New rules affect Egyptians living in the United Arab Emirates.");
        var uae = CountryWithCities("الإمارات", "United Arab Emirates", "AE", "uae", NewCity("دبي", "Dubai", "dubai"));
        var extractor = new RulesBasedGeographicExtractor();

        var result = await extractor.ExtractAsync(story, [uae], default);

        result.CountryId.Should().Be(uae.Id);
        result.CityId.Should().BeNull();
        result.Reasons.Should().Contain("United Arab Emirates");
    }

    [Fact]
    public async Task ExtractAsync_should_prefer_a_city_match_over_a_country_match()
    {
        var dubai = NewCity("دبي", "Dubai", "dubai");
        var uae = CountryWithCities("الإمارات", "United Arab Emirates", "AE", "uae", dubai);
        var story = StoryWithArticle(
            "Egyptian man arrested in Dubai",
            "An Egyptian expatriate was arrested by Dubai police, the Egyptian consulate confirmed.");

        var extractor = new RulesBasedGeographicExtractor();
        var result = await extractor.ExtractAsync(story, [uae], default);

        // "Egyptian" appears more often in the text than "Dubai", but the
        // city-level signal must still win — see the extractor's own doc
        // comment for why.
        result.CountryId.Should().Be(uae.Id);
        result.CityId.Should().Be(dubai.Id);
        result.Reasons.Should().Contain("Dubai");
    }

    [Fact]
    public async Task ExtractAsync_should_pick_the_country_mentioned_most_when_several_are_named()
    {
        var uae = CountryWithCities("الإمارات", "United Arab Emirates", "AE", "uae");
        var uk = CountryWithCities("بريطانيا", "United Kingdom", "GB", "uk");
        var story = StoryWithArticle(
            "Egyptians abroad react to new policy",
            "The United Kingdom announced a policy change. The United Kingdom's Home Office confirmed the details, while the United Arab Emirates made no comment.");

        var extractor = new RulesBasedGeographicExtractor();
        var result = await extractor.ExtractAsync(story, [uae, uk], default);

        result.CountryId.Should().Be(uk.Id);
        result.CityId.Should().BeNull();
    }

    [Fact]
    public async Task ExtractAsync_should_consider_every_clustered_article_not_just_the_first()
    {
        var now = DateTimeOffset.UtcNow;
        var story = Story.Create("Neutral headline", now);

        var firstArticle = Article.Create(
            Guid.NewGuid(), "ext-1", "https://a.example.com/1", "Neutral headline",
            new string('a', 64), null, now, "No place mentioned here.");
        firstArticle.AssignToStory(story, now);

        var secondArticle = Article.Create(
            Guid.NewGuid(), "ext-2", "https://b.example.com/1", "Different outlet's headline",
            new string('b', 64), null, now, "But this source says it happened in Dubai.");
        secondArticle.AssignToStory(story, now);

        var dubai = NewCity("دبي", "Dubai", "dubai");
        var uae = CountryWithCities("الإمارات", "United Arab Emirates", "AE", "uae", dubai);

        var extractor = new RulesBasedGeographicExtractor();
        var result = await extractor.ExtractAsync(story, [uae], default);

        result.CityId.Should().Be(dubai.Id);
    }
}
