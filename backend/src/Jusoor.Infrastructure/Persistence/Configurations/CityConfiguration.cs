using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("Cities");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(100);
        builder.Property(c => c.NameEn).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Latitude).HasPrecision(9, 6);
        builder.Property(c => c.Longitude).HasPrecision(9, 6);

        // Slugs are unique per country, not globally — "London" as a slug
        // could plausibly collide across countries in future data.
        builder.HasIndex(c => new { c.CountryId, c.Slug }).IsUnique();
    }
}
