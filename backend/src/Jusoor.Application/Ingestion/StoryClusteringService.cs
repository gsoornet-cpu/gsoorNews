using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Ingestion;

/// <summary>
/// Exact-duplicate (ContentHash-based) cross-source clustering only. This is
/// deliberately NOT where near-duplicate/semantic clustering happens — that
/// needs embeddings (pgvector), which the roadmap explicitly defers until a
/// column exists AND is actually read/written by real logic (no "looks
/// useful" columns — see the roadmap's own note on why an Embedding column
/// was deliberately not added in Slice 1/2). Matching purely on ContentHash
/// equality here is a
/// conservative, explainable rule: it only clusters Articles whose
/// normalized title+content are byte-for-byte identical, which is exactly
/// the "different Source republished the same wire text" case §12
/// describes as "exact duplicates" — never a semantic/topical match.
/// </summary>
public class StoryClusteringService : IStoryClusteringService
{
    private readonly IApplicationDbContext _context;

    public StoryClusteringService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Story> ClusterAsync(Article article, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        // Look for any other Article (any Source, including this same one —
        // a source legitimately re-publishing its own wire content under a
        // new item id is still the same real-world Story) that already
        // carries this exact ContentHash AND has already been clustered.
        // Compared by Id (client-generated at construction, see BaseEntity)
        // rather than by reference, so this excludes `article` itself even
        // though it may already be tracked (Added) by this same DbContext
        // at this point.
        var existingStoryId = await _context.Articles
            .Where(a => a.ContentHash == article.ContentHash && a.StoryId != null && a.Id != article.Id)
            .Select(a => a.StoryId)
            .FirstOrDefaultAsync(cancellationToken);

        Story story;
        if (existingStoryId is Guid storyId)
        {
            // Fetch the tracked/existing Story rather than constructing a
            // stub — AttachArticle (called via AssignToStory below) mutates
            // LastUpdatedAtUtc on the real entity, which must be the one
            // EF Core actually persists.
            story = await _context.Stories.FirstAsync(s => s.Id == storyId, cancellationToken);
        }
        else
        {
            // First time this content has ever been seen — this Article
            // becomes the seed of a brand-new Story. The Story's canonical
            // title starts as this Article's title; refining/normalizing
            // canonical titles once multiple Articles disagree is Relevance
            // Engine scope, not this stage's job.
            story = Story.Create(article.Title, nowUtc);
            _context.Stories.Add(story);
        }

        article.AssignToStory(story, nowUtc);
        return story;
    }
}
