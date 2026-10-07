using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class RelevanceResultConfiguration : IEntityTypeConfiguration<RelevanceResult>
{
    public void Configure(EntityTypeBuilder<RelevanceResult> builder)
    {
        builder.ToTable("RelevanceResults");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ConfidenceScore).HasPrecision(3, 2);
        builder.Property(r => r.Method).IsRequired().HasConversion<int>();
        builder.Property(r => r.Reasons).IsRequired().HasMaxLength(2000);
        builder.Property(r => r.EngineVersion).IsRequired().HasMaxLength(50);

        builder.HasOne<Story>()
            .WithMany()
            .HasForeignKey(r => r.StoryId)
            .OnDelete(DeleteBehavior.Cascade); // a Story's evaluation history has no meaning without the Story

        builder.HasOne<Article>()
            .WithMany()
            .HasForeignKey(r => r.TriggeringArticleId)
            .OnDelete(DeleteBehavior.SetNull); // keep the audit row even if the triggering article is later purged

        // "Give me the current/latest verdict for this Story" is the
        // dominant read pattern (Story.Status is denormalized for the
        // common case, but anything auditing/debugging goes through here).
        builder.HasIndex(r => new { r.StoryId, r.EvaluatedAtUtc });
    }
}
