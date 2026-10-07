namespace Jusoor.Application.Geography.Contracts;

/// <summary>
/// The result of one geographic-extraction pass over a Story. Deliberately
/// has no confidence score, unlike RelevanceEvaluation — Story.SetGeography
/// takes plain nullable ids with no threshold/review concept, so there's
/// nothing downstream that would consume a confidence number yet. If a
/// later slice adds a geography-review workflow, add it then (see the
/// project's own "never add infrastructure before something real uses it"
/// principle, already applied to RelevanceEvaluation's Embeddings/Llm
/// methods).
/// </summary>
public sealed record GeographicExtractionResult(
    Guid? CountryId,
    Guid? CityId,
    string Reasons,
    string ExtractorVersion);
