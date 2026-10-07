using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class RoleChangeAuditConfiguration : IEntityTypeConfiguration<RoleChangeAudit>
{
    public void Configure(EntityTypeBuilder<RoleChangeAudit> builder)
    {
        builder.ToTable("RoleChangeAudits");
        builder.HasKey(a => a.Id);

        // Plain strings, not FKs to AspNetUsers — same convention as
        // HumanReviewDecision.ReviewedByUserId — and deliberately so here: the
        // audit row must survive even if a user account is later deleted.
        builder.Property(a => a.ActorUserId).IsRequired().HasMaxLength(450);
        builder.Property(a => a.TargetUserId).IsRequired().HasMaxLength(450);
        builder.Property(a => a.Role).IsRequired().HasMaxLength(64);
        builder.Property(a => a.Action).IsRequired().HasConversion<int>();

        builder.HasIndex(a => new { a.TargetUserId, a.OccurredAtUtc });
        builder.HasIndex(a => new { a.ActorUserId, a.OccurredAtUtc });
    }
}
