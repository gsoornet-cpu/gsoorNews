using Jusoor.Domain.Entities;

namespace Jusoor.Application.Common.Interfaces;

/// <summary>
/// Attaches a freshly-fetched Article to a Story — either an existing one
/// (cross-source exact-duplicate: the same content already arrived from a
/// different Source) or a brand-new one created for it. This is the
/// "Cross-Source Deduplication" pipeline stage from the roadmap's remaining
/// Phase 1 scope (§12's exact-duplicate half; near-duplicate/semantic
/// clustering via embeddings is a separate, later Phase 4 slice — see
/// StoryClusteringService's own remarks for why the two must not be
/// conflated).
/// </summary>
public interface IStoryClusteringService
{
    /// <summary>
    /// Determines which Story the given Article belongs to and calls
    /// Article.AssignToStory on it. The Article must already be tracked by
    /// the DbContext (Added) before this is called; this method does not
    /// call SaveChangesAsync itself — the caller controls the save/retry
    /// boundary (see IngestSourceCommandHandler's per-item save + duplicate-
    /// key-race handling for why).
    /// </summary>
    Task<Story> ClusterAsync(Article article, DateTimeOffset nowUtc, CancellationToken cancellationToken);
}
