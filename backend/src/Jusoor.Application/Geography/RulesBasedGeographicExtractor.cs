using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Geography.Contracts;
using Jusoor.Domain.Entities;

namespace Jusoor.Application.Geography;

/// <summary>
/// Geographic Extraction v1 — spec §06's "استخراج الموقع" stage, explicitly
/// listed as one of the deterministic (no-LLM) pipeline stages. Matches the
/// Story's combined Article text against the known Country/City gazetteer
/// by name — no NER model, no geocoding API, both deliberately deferred to
/// Phase 2's richer geo-intelligence work (same "don't build infrastructure
/// before something real needs it" call as RulesBasedRelevanceEngine's
/// deferred Embeddings/LLM halves).
///
/// A City match always wins over a bare Country match, even when the
/// Country name is mentioned more often — for a diaspora platform, "an
/// Egyptian man was arrested in Dubai" is about Dubai/UAE (the host
/// country, where the event happened), not Egypt (the nationality, already
/// captured separately by the Relevance Engine). The more specific signal
/// wins, same philosophy as RulesBasedRelevanceEngine's
/// StrongSignals > NationalitySignals > AmbiguousPlaceSignals ranking.
///
/// IsoCode2 is deliberately NOT matched against — a bare 2-letter code
/// ("AE", "US") inside free-text news copy is far too likely to collide
/// with an unrelated word/abbreviation to be a safe deterministic signal.
/// </summary>
public class RulesBasedGeographicExtractor : IGeographicExtractor
{
    public const string ExtractorVersion = "rules-geo-v1";

    public Task<GeographicExtractionResult> ExtractAsync(
        Story story, IReadOnlyList<Country> knownCountries, CancellationToken cancellationToken)
    {
        var corpus = BuildCorpus(story);

        if (string.IsNullOrWhiteSpace(corpus) || knownCountries.Count == 0)
        {
            return Task.FromResult(new GeographicExtractionResult(
                CountryId: null,
                CityId: null,
                Reasons: "No article text and/or no known Country/City gazetteer to match against.",
                ExtractorVersion: ExtractorVersion));
        }

        // Deterministic ordering (by NameEn) so that a tie in occurrence
        // count always resolves to the same winner regardless of the
        // gazetteer's DB-query return order — matters for reproducible
        // extraction, same as the audit-trail concern behind
        // RelevanceResult being append-only.
        var orderedCountries = knownCountries.OrderBy(c => c.NameEn, StringComparer.OrdinalIgnoreCase).ToList();

        var cityMatches = orderedCountries
            .SelectMany(country => country.Cities.Select(city => (country, city)))
            .OrderBy(pair => pair.city.NameEn, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new
            {
                pair.country,
                pair.city,
                Occurrences = CountOccurrences(corpus, pair.city.NameAr) + CountOccurrences(corpus, pair.city.NameEn)
            })
            .Where(m => m.Occurrences > 0)
            .OrderByDescending(m => m.Occurrences)
            .ToList();

        if (cityMatches.Count > 0)
        {
            var winner = cityMatches[0];
            return Task.FromResult(new GeographicExtractionResult(
                CountryId: winner.country.Id,
                CityId: winner.city.Id,
                Reasons: $"Matched city '{winner.city.NameEn}' ({winner.Occurrences} occurrence(s)) — city-level match takes priority over any bare country mention.",
                ExtractorVersion: ExtractorVersion));
        }

        var countryMatches = orderedCountries
            .Select(country => new
            {
                country,
                Occurrences = CountOccurrences(corpus, country.NameAr) + CountOccurrences(corpus, country.NameEn)
            })
            .Where(m => m.Occurrences > 0)
            .OrderByDescending(m => m.Occurrences)
            .ToList();

        if (countryMatches.Count > 0)
        {
            var winner = countryMatches[0];
            return Task.FromResult(new GeographicExtractionResult(
                CountryId: winner.country.Id,
                CityId: null,
                Reasons: $"Matched country '{winner.country.NameEn}' ({winner.Occurrences} occurrence(s)); no known city name matched.",
                ExtractorVersion: ExtractorVersion));
        }

        return Task.FromResult(new GeographicExtractionResult(
            CountryId: null,
            CityId: null,
            Reasons: "No known Country or City name matched the article text.",
            ExtractorVersion: ExtractorVersion));
    }

    /// <summary>Case-insensitive, non-overlapping occurrence count. A plain
    /// substring count (not word-boundary-aware) — same simplicity level as
    /// RulesBasedRelevanceEngine's Contains-based matching, and Arabic text
    /// doesn't reliably tokenize on the same word-boundary rules .NET's
    /// regex engine assumes for Latin scripts, so a naive \b-based regex
    /// would silently under-match Arabic city/country names.</summary>
    private static int CountOccurrences(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle))
        {
            return 0;
        }

        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static string BuildCorpus(Story story)
    {
        // Same corpus-building approach as RulesBasedRelevanceEngine —
        // deliberately duplicated here rather than extracted to a shared
        // helper, since the two engines currently have no other shared
        // dependency and this is 3 lines; factor it out only when a third
        // consumer needs the same logic (YAGNI over premature sharing).
        var parts = story.Articles.SelectMany(a => new[] { a.Title, a.Content ?? string.Empty });
        return string.Join(" \n ", parts);
    }
}
