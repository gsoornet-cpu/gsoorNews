using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class SourceFetchLogConfiguration : IEntityTypeConfiguration<SourceFetchLog>
{
    public void Configure(EntityTypeBuilder<SourceFetchLog> builder)
    {
        builder.ToTable("SourceFetchLogs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Outcome).IsRequired().HasConversion<int>();
        builder.Property(l => l.ErrorSummary).HasMaxLength(1000);

        builder.HasOne<Source>()
            .WithMany()
            .HasForeignKey(l => l.SourceId)
            .OnDelete(DeleteBehavior.Cascade); // fetch history has no meaning without the Source

        // "Recent attempts for this source" (health dashboards, alerting on
        // repeated failures) is the dominant read pattern.
        builder.HasIndex(l => new { l.SourceId, l.StartedAtUtc });
    }
}
