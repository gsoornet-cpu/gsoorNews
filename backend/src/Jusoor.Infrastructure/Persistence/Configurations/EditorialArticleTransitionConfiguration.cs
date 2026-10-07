using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class EditorialArticleTransitionConfiguration : IEntityTypeConfiguration<EditorialArticleTransition>
{
    public void Configure(EntityTypeBuilder<EditorialArticleTransition> builder)
    {
        builder.ToTable("EditorialArticleTransitions");
        builder.HasKey(t => t.Id);

        // Plain Guid / strings, no foreign keys — same convention as
        // RoleChangeAudit: the audit trail must outlive whatever it describes.
        builder.Property(t => t.ArticleId).IsRequired();
        builder.Property(t => t.FromStatus).IsRequired().HasConversion<int>();
        builder.Property(t => t.ToStatus).IsRequired().HasConversion<int>();
        builder.Property(t => t.ActorUserId).IsRequired().HasMaxLength(450);
        builder.Property(t => t.ActorRoles).IsRequired().HasMaxLength(EditorialArticleTransition.ActorRolesMaxLength);
        builder.Property(t => t.Reason).HasMaxLength(EditorialArticleTransition.ReasonMaxLength);

        builder.HasIndex(t => new { t.ArticleId, t.OccurredAtUtc });
        builder.HasIndex(t => new { t.ActorUserId, t.OccurredAtUtc });
    }
}
