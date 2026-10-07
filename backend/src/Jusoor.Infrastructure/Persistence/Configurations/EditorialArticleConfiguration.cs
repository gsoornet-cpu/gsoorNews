using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class EditorialArticleConfiguration : IEntityTypeConfiguration<EditorialArticle>
{
    public void Configure(EntityTypeBuilder<EditorialArticle> builder)
    {
        // Slice 20b: a Retracted row (Status = 6) without a public notice must be
        // impossible even if application code is wrong — the notice is the only
        // thing readers see in place of the article.
        builder.ToTable("EditorialArticles", t =>
        {
            t.HasCheckConstraint(
                "CK_EditorialArticles_RetractedHasNotice",
                "\"Status\" <> 6 OR \"RetractionNotice\" IS NOT NULL");
            t.HasCheckConstraint(
                "CK_EditorialArticles_ScheduledHasPublishTime",
                "(\"Status\" = 5 AND \"ScheduledPublishAtUtc\" IS NOT NULL) OR (\"Status\" <> 5 AND \"ScheduledPublishAtUtc\" IS NULL)");
        });
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).IsRequired().HasMaxLength(EditorialArticle.TitleMaxLength);
        builder.Property(a => a.AuthorName).HasMaxLength(120);
        builder.Property(a => a.Slug).IsRequired().HasMaxLength(180);
        builder.HasIndex(a => a.Slug).IsUnique();
        builder.Property(a => a.SeoTitle).HasMaxLength(60);
        builder.Property(a => a.SeoDescription).HasMaxLength(160);
        builder.Property(a => a.CanonicalUrl).HasMaxLength(2048);
        builder.Property(a => a.SocialTitle).HasMaxLength(300);
        builder.Property(a => a.SocialDescription).HasMaxLength(500);
        builder.Property(a => a.SocialImageUrl).HasMaxLength(2048);
        builder.Property(a => a.TwitterTitle).HasMaxLength(60);
        builder.Property(a => a.TwitterDescription).HasMaxLength(160);
        builder.Property(a => a.TwitterImageUrl).HasMaxLength(2048);
        builder.Property(a => a.SecondaryTags).HasColumnType("text[]").IsRequired();
        builder.Property(a => a.PresentationDesks).HasColumnType("text[]").IsRequired();
        builder.HasOne(a => a.FeaturedVideoMediaAsset).WithMany().HasForeignKey(a => a.FeaturedVideoMediaAssetId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(a => a.FeaturedVideoMediaAssetId);
        builder.HasOne(a => a.PrimaryCategory).WithMany().HasForeignKey(a => a.PrimaryCategoryId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(a => a.PrimaryCategoryId);
        builder.Property(a => a.Summary).HasMaxLength(EditorialArticle.SummaryMaxLength);
        builder.Property(a => a.Body).IsRequired(); // unbounded text; length capped in the domain
        // 0 = PlainText (every pre-Slice-21 row — the migration back-fills with the
        // column's zero default), 1 = Html. No HasDefaultValue on purpose: new rows
        // always carry an explicit value from the domain.
        builder.Property(a => a.BodyFormat).IsRequired().HasConversion<int>();
        // Status is the aggregate's workflow concurrency token: competing
        // transitions cannot overwrite a successful scheduled publish/cancel.
        builder.Property(a => a.Status).IsRequired().HasConversion<int>().IsConcurrencyToken();
        builder.HasIndex(a => new { a.Status, a.ScheduledPublishAtUtc });

        // Plain strings, not FKs to AspNetUsers — same convention as
        // AuditableEntity's user-id columns and Case.AssignedToCrisisEditorUserId.
        builder.Property(a => a.OwnerUserId).IsRequired().HasMaxLength(450);
        builder.Property(a => a.PublishedByUserId).HasMaxLength(450);
        builder.Property(a => a.ScheduledPublishAtUtc);

        // Slice 20b (nullable, no defaults: existing rows are unaffected).
        builder.Property(a => a.RetractedByUserId).HasMaxLength(450);
        builder.Property(a => a.ArchivedByUserId).HasMaxLength(450);
        builder.Property(a => a.RetractionNotice).HasMaxLength(EditorialArticle.RetractionNoticeMaxLength);

        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(a => a.CountryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(a => a.CityId)
            .OnDelete(DeleteBehavior.SetNull);

        // Public feed: WHERE Status = Published ORDER BY PublishedAtUtc DESC.
        builder.HasIndex(a => new { a.Status, a.PublishedAtUtc });

        // Reporter's "my articles" list.
        builder.HasIndex(a => a.OwnerUserId);
    }
}
