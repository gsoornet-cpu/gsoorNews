using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class StoryConfiguration : IEntityTypeConfiguration<Story>
{
    public void Configure(EntityTypeBuilder<Story> builder)
    {
        builder.ToTable("Stories");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.CanonicalTitle).IsRequired().HasMaxLength(500);
        builder.Property(s => s.Status).IsRequired().HasConversion<int>();

        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(s => s.PrimaryCountryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(s => s.PrimaryCityId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(s => s.Articles)
            .WithOne()
            .HasForeignKey(a => a.StoryId)
            .OnDelete(DeleteBehavior.Restrict); // an Article's history must survive even if a Story is ever archived

        // Recency queries (homepage, dashboards) are the dominant read
        // pattern once real traffic exists — index it now rather than
        // discovering the missing index under load in Phase 2.
        builder.HasIndex(s => s.LastUpdatedAtUtc);
        builder.HasIndex(s => s.Status);
    }
}
