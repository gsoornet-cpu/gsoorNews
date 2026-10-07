using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Common.Interfaces;

/// <summary>
/// The Application layer depends on this interface, never on
/// Infrastructure's concrete DbContext. This is what keeps handlers testable
/// without a real Postgres instance (swap in an InMemory or SQLite provider
/// bound to this same interface in unit tests) and keeps the dependency
/// arrow pointing inward, per Clean Architecture.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Country> Countries { get; }
    DbSet<Region> Regions { get; }
    DbSet<City> Cities { get; }
    DbSet<Neighborhood> Neighborhoods { get; }
    DbSet<Source> Sources { get; }
    DbSet<Story> Stories { get; }
    DbSet<Article> Articles { get; }
    DbSet<RelevanceResult> RelevanceResults { get; }
    DbSet<HumanReviewDecision> HumanReviewDecisions { get; }
    DbSet<DiasporaProfile> DiasporaProfiles { get; }
    DbSet<MyPlace> MyPlaces { get; }
    DbSet<SourceFetchLog> SourceFetchLogs { get; }
    DbSet<Case> Cases { get; }
    DbSet<EditorialArticle> EditorialArticles { get; }
    DbSet<EditorialCategory> EditorialCategories { get; }
    DbSet<RoleChangeAudit> RoleChangeAudits { get; }
    DbSet<EditorialArticleTransition> EditorialArticleTransitions { get; }
    DbSet<EditorialArticleRevision> EditorialArticleRevisions { get; }
    DbSet<EditorialCorrection> EditorialCorrections { get; }
    DbSet<EditorialMediaAsset> EditorialMediaAssets { get; }

    /// <summary>Detaches an entity from change tracking. Used by
    /// IngestSourceCommandHandler to recover from a per-item duplicate-key
    /// save failure without re-attempting to insert that same entity on the
    /// next SaveChangesAsync call in the same batch. Exposed here (rather
    /// than casting to the concrete DbContext in Application code) so the
    /// Application layer never needs to know EF Core's ChangeTracker API.</summary>
    void Detach<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
