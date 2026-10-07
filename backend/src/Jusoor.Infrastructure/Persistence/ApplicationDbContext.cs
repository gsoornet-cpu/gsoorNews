using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Common;
using Jusoor.Domain.Entities;
using Jusoor.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Jusoor.Infrastructure.Persistence;

/// <summary>
/// Extends IdentityDbContext&lt;ApplicationUser&gt; so Identity's own tables
/// (AspNetUsers, AspNetRoles, ...) live in the same database/schema as the
/// domain tables, migrated together. Using string keys (Identity's default)
/// rather than Guid keys for Identity tables specifically, to avoid fighting
/// the framework's own conventions for no real benefit.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IFieldEncryptionService _fieldEncryptionService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUserService,
        IFieldEncryptionService fieldEncryptionService)
        : base(options)
    {
        _currentUserService = currentUserService;
        _fieldEncryptionService = fieldEncryptionService;
    }

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
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<EditorialArticle> EditorialArticles => Set<EditorialArticle>();
    public DbSet<EditorialCategory> EditorialCategories => Set<EditorialCategory>();
    public DbSet<RoleChangeAudit> RoleChangeAudits => Set<RoleChangeAudit>();
    public DbSet<EditorialArticleTransition> EditorialArticleTransitions => Set<EditorialArticleTransition>();
    public DbSet<EditorialArticleRevision> EditorialArticleRevisions => Set<EditorialArticleRevision>();
    public DbSet<EditorialCorrection> EditorialCorrections => Set<EditorialCorrection>();
    public DbSet<EditorialMediaAsset> EditorialMediaAssets => Set<EditorialMediaAsset>();

    public void Detach<TEntity>(TEntity entity) where TEntity : class => Entry(entity).State = EntityState.Detached;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity's own configuration first

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        ConfigureCaseFieldEncryption(builder);
    }

    /// <summary>
    /// Case's encrypted-column value converters, wired here rather than in
    /// CaseConfiguration.cs — see that file's own comment for why
    /// IEntityTypeConfiguration can't receive the injected
    /// IFieldEncryptionService via ApplyConfigurationsFromAssembly's
    /// parameterless-constructor requirement. This is the one place with
    /// access to both the model builder and the constructor-injected service.
    ///
    /// IFieldEncryptionService is registered as a DI singleton specifically
    /// so this is safe: EF Core builds and caches its model once per
    /// DbContext type (not once per instance), so whichever
    /// ApplicationDbContext instance happens to trigger the first model
    /// build would otherwise "freeze in" its own injected instance forever.
    /// A singleton has no per-request state to freeze incorrectly — see
    /// IFieldEncryptionService's own doc comment on this exact constraint.
    /// </summary>
    private void ConfigureCaseFieldEncryption(ModelBuilder builder)
    {
        var stringConverter = new ValueConverter<string, string>(
            plaintext => _fieldEncryptionService.Encrypt(plaintext),
            ciphertext => _fieldEncryptionService.Decrypt(ciphertext));

        var nullableStringConverter = new ValueConverter<string?, string?>(
            plaintext => plaintext == null ? null : _fieldEncryptionService.Encrypt(plaintext),
            ciphertext => ciphertext == null ? null : _fieldEncryptionService.Decrypt(ciphertext));

        builder.Entity<Case>(b =>
        {
            b.Property(c => c.SubjectName).HasConversion(stringConverter);
            b.Property(c => c.ConcernDescription).HasConversion(stringConverter);
            b.Property(c => c.ApplicantPhoneE164).HasConversion(stringConverter);
            b.Property(c => c.SubjectContactPhone).HasConversion(nullableStringConverter);
        });
    }

    /// <summary>
    /// Stamps AuditableEntity's Created*/LastModified* fields automatically.
    /// Previously nothing populated these — every AuditableEntity (Source in
    /// Phase 0, Story/Article in this slice) would have silently persisted
    /// with default(DateTimeOffset)/null forever. This is the fix, applied
    /// once here rather than per-handler, so no future entity can forget it.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = _currentUserService.UserId;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.CreatedByUserId = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.LastModifiedAtUtc = now;
                    entry.Entity.LastModifiedByUserId = userId;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
