using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.UnitTests.TestSupport;

public class TestApplicationDbContext : DbContext, IApplicationDbContext
{
    public TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options) : base(options) { }

    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Neighborhood> Neighborhoods => Set<Neighborhood>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<Story> Stories => Set<Story>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<RelevanceResult> RelevanceResults => Set<RelevanceResult>();
    public DbSet<HumanReviewDecision> HumanReviewDecisions => Set<HumanReviewDecision>();
    public DbSet<DiasporaProfile> DiasporaProfiles => Set<DiasporaProfile>();
    public DbSet<MyPlace> MyPlaces => Set<MyPlace>();
    public DbSet<SourceFetchLog> SourceFetchLogs => Set<SourceFetchLog>();

    // Interface-completion only — no test here exercises Case yet. Real
    // encrypted-column behavior is Infrastructure's ApplicationDbContext +
    // CaseConfiguration, not reproduced here, same as this class's own
    // comment already says for every other entity's real configuration.
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<EditorialArticle> EditorialArticles => Set<EditorialArticle>();
    public DbSet<RoleChangeAudit> RoleChangeAudits => Set<RoleChangeAudit>();
    public DbSet<EditorialArticleTransition> EditorialArticleTransitions => Set<EditorialArticleTransition>();
    public DbSet<EditorialArticleRevision> EditorialArticleRevisions => Set<EditorialArticleRevision>();
    public DbSet<EditorialCorrection> EditorialCorrections => Set<EditorialCorrection>();
    public DbSet<EditorialMediaAsset> EditorialMediaAssets => Set<EditorialMediaAsset>();
    public DbSet<EditorialCategory> EditorialCategories => Set<EditorialCategory>();

    public void Detach<TEntity>(TEntity entity) where TEntity : class => Entry(entity).State = EntityState.Detached;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Only what IngestSourceCommandHandlerTests actually exercises —
        // this is NOT a substitute for Infrastructure's real
        // ArticleConfiguration/etc., which remain the source of truth for
        // the actual schema (see StoryConfiguration.cs and siblings).
        modelBuilder.Entity<Article>().HasIndex(a => new { a.SourceId, a.ExternalSourceItemId }).IsUnique();
    }

    public static TestApplicationDbContext CreateNew()
    {
        var builder = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging();

        return new TestApplicationDbContext(builder.Options);
    }
}
