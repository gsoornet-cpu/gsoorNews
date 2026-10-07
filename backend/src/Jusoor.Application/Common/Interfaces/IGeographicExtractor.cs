using Jusoor.Application.Geography.Contracts;
using Jusoor.Domain.Entities;

namespace Jusoor.Application.Common.Interfaces;

/// <summary>
/// Determines which Country/City a Story is primarily about — spec §06's
/// "استخراج الموقع" (location extraction) pipeline stage, one of the
/// explicitly "deterministic, no LLM" stages. Async even though the v1
/// (rules-based) implementation is pure in-memory computation, for the
/// same reason IRelevanceEngine is: a later stage that resolves ambiguous
/// place names via a geocoding API or NER model (Phase 2's richer
/// geo-intelligence work) will need real I/O, and keeping the interface
/// async now avoids a breaking signature change when that lands.
///
/// Takes the known Country/City gazetteer as a parameter rather than
/// reading IApplicationDbContext itself — same reasoning as
/// StoryClusteringService taking its dependencies explicitly: it keeps
/// this class pure/deterministic and trivially unit-testable with
/// in-memory fixture data, and lets the caller (IngestSourceCommandHandler)
/// own the one DB round-trip per ingestion run instead of one per Story.
/// </summary>
public interface IGeographicExtractor
{
    Task<GeographicExtractionResult> ExtractAsync(
        Story story, IReadOnlyList<Country> knownCountries, CancellationToken cancellationToken);
}
