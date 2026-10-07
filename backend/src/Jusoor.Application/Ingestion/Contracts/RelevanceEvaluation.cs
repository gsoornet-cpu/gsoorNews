using Jusoor.Domain.Enums;

namespace Jusoor.Application.Relevance.Contracts;

/// <summary>
/// The result of one relevance-engine pass over a Story. Mirrors exactly
/// what RelevanceResult.Create and Story.ApplyRelevanceVerdict need — this
/// is the seam between "the engine decided something" and "that decision
/// got persisted/applied", kept as a plain contract so a future
/// embeddings/LLM engine (RelevanceMethod.Embeddings / .Llm) can return the
/// same shape without IngestSourceCommandHandler needing to change.
/// </summary>
public sealed record RelevanceEvaluation(
    bool IsRelevant,
    decimal ConfidenceScore,
    RelevanceMethod Method,
    string Reasons,
    string EngineVersion);