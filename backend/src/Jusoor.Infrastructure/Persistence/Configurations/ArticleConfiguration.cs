using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("Articles");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ExternalSourceItemId).IsRequired().HasMaxLength(500);
        builder.Property(a => a.CanonicalUrl).IsRequired().HasMaxLength(2000);
        builder.Property(a => a.Title).IsRequired().HasMaxLength(500);
        builder.Property(a => a.ContentHash).IsRequired().HasMaxLength(64); // SHA-256 hex
        // Unbounded body text (Slice 5) — "text" rather than a capped
        // varchar since article bodies have no natural length ceiling.
        builder.Property(a => a.Content).HasColumnType("text");
        builder.Property(a => a.DetectedLanguage).HasMaxLength(10);
        builder.Property(a => a.FailureReason).HasMaxLength(1000);
        builder.Property(a => a.Status).IsRequired().HasConversion<int>();

        builder.HasOne<Source>()
            .WithMany()
            .HasForeignKey(a => a.SourceId)
            .OnDelete(DeleteBehavior.Restrict); // a source's articles must survive if the source is later deactivated

        // Idempotency key #1: the same source can never produce two rows
        // for the same upstream item, even under concurrent/retried
        // ingestion runs. Enforced in the database, not just in application
        // logic, per §18.
        builder.HasIndex(a => new { a.SourceId, a.ExternalSourceItemId }).IsUnique();

        // Idempotency key #2: exact-duplicate content detection (§12,
        // "exact duplicates"). Deliberately NOT unique across the whole
        // table by itself in isolation from SourceId — two different
        // sources legitimately republishing the same wire-service text is
        // an expected, valid case (that's near-duplicate clustering, a
        // later slice), not a constraint violation.
        builder.HasIndex(a => new { a.SourceId, a.ContentHash });

        builder.HasIndex(a => a.StoryId);
        builder.HasIndex(a => a.Status);
    }
}