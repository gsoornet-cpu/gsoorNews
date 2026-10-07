using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class EditorialArticleRevisionConfiguration : IEntityTypeConfiguration<EditorialArticleRevision>
{
    public void Configure(EntityTypeBuilder<EditorialArticleRevision> builder)
    {
        builder.ToTable("EditorialArticleRevisions");
        builder.HasKey(r => r.Id);

        // No foreign keys (same convention as EditorialArticleTransition): the
        // history must outlive whatever it describes. CountryId/CityId are
        // snapshot values, not live references.
        builder.Property(r => r.ArticleId).IsRequired();
        builder.Property(r => r.RevisionNumber).IsRequired();
        builder.Property(r => r.Title).IsRequired().HasMaxLength(Jusoor.Domain.Entities.EditorialArticle.TitleMaxLength);
        builder.Property(r => r.AuthorName).HasMaxLength(120);
        builder.Property(r => r.Summary).HasMaxLength(Jusoor.Domain.Entities.EditorialArticle.SummaryMaxLength);
        builder.Property(r => r.Body).IsRequired();
        builder.Property(r => r.BodyFormat).IsRequired().HasConversion<int>(); // see EditorialArticleConfiguration
        builder.Property(r => r.EditedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(r => r.EditorRoles).IsRequired().HasMaxLength(EditorialArticleRevision.ActorRolesMaxLength);
        builder.Property(r => r.Reason).HasMaxLength(EditorialArticleRevision.ReasonMaxLength);

        // One revision number per article — also makes a lost race between two
        // concurrent edits fail loudly instead of writing a duplicate number.
        builder.HasIndex(r => new { r.ArticleId, r.RevisionNumber }).IsUnique();
        builder.HasIndex(r => new { r.EditedByUserId, r.EditedAtUtc });
    }
}
