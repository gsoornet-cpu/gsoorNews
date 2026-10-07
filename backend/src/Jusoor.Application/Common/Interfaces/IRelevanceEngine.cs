using Jusoor.Application.Relevance.Contracts;
using Jusoor.Domain.Entities;

namespace Jusoor.Application.Common.Interfaces;

/// <summary>
/// Evaluates whether a Story is relevant to Egyptians abroad (spec §07 —
/// "the one question that decides everything"). Async even though the v1
/// (rules-based) implementation is pure in-memory computation, because a
/// later embeddings-based implementation (RelevanceMethod.Embeddings) will
/// need pgvector similarity lookups, and an LLM-escalation implementation
/// (RelevanceMethod.Llm) will need an actual API call — keeping the
/// interface async now avoids a breaking signature change when those
/// slices arrive.
/// </summary>
public interface IRelevanceEngine
{
    Task<RelevanceEvaluation> EvaluateAsync(Story story, CancellationToken cancellationToken);
}