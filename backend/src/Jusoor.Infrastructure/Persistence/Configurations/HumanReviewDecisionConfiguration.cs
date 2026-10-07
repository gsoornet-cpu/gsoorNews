using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class HumanReviewDecisionConfiguration : IEntityTypeConfiguration<HumanReviewDecision>
{
    public void Configure(EntityTypeBuilder<HumanReviewDecision> builder)
    {
        builder.ToTable("HumanReviewDecisions");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.ReviewedByUserId).IsRequired();
        builder.Property(d => d.Reasoning).IsRequired().HasMaxLength(2000);

        builder.HasOne<Story>()
            .WithMany()
            .HasForeignKey(d => d.StoryId)
            .OnDelete(DeleteBehavior.Cascade); // an editorial audit row has no meaning without the Story it's about

        // Deliberately NOT a foreign key onto AspNetUsers: ApplicationUser
        // lives in the Infrastructure/Identity slice of the model, and nothing
        // else in this codebase FK's a domain entity to it directly (see
        // AuditableEntity.CreatedByUserId/LastModifiedByUserId — plain
        // strings, no navigation). Consistent with that established
        // precedent rather than introducing a new cross-boundary FK style.

        // "Who reviewed what, when" and "show me this Story's full review
        // history" are the two read patterns this index serves — the same
        // shape as RelevanceResultConfiguration's (StoryId, EvaluatedAtUtc)
        // index, for the same reason.
        builder.HasIndex(d => new { d.StoryId, d.DecidedAtUtc });
        builder.HasIndex(d => new { d.ReviewedByUserId, d.DecidedAtUtc });
    }
}
