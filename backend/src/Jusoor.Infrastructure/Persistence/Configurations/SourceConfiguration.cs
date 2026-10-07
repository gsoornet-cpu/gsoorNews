using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class SourceConfiguration : IEntityTypeConfiguration<Source>
{
    public void Configure(EntityTypeBuilder<Source> builder)
    {
        builder.ToTable("Sources");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Layer).IsRequired().HasConversion<int>();
        builder.Property(s => s.ReliabilityScore).HasPrecision(3, 2);
        builder.Property(s => s.FeedUrl).HasMaxLength(500);
        builder.Property(s => s.OfficialWebsiteUrl).HasMaxLength(500);

        builder.HasIndex(s => s.Layer);
    }
}
