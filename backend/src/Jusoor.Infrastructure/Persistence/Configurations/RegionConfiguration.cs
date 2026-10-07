using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("Regions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.NameAr).IsRequired().HasMaxLength(100);
        builder.Property(r => r.NameEn).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Slug).IsRequired().HasMaxLength(100);

        // Same reasoning as City's own (CountryId, Slug) index — "Central"
        // as a region slug could plausibly collide across countries.
        builder.HasIndex(r => new { r.CountryId, r.Slug }).IsUnique();

        builder.HasOne(r => r.Country)
            .WithMany(c => c.Regions)
            .HasForeignKey(r => r.CountryId)
            .OnDelete(DeleteBehavior.Restrict); // same "never cascade-delete geography other data depends on" rule as Country→City

        // City.RegionId is nullable (see City.cs) — this is genuinely an
        // optional relationship, so Restrict here means "you can't delete a
        // Region while Cities still point to it," not "every City must
        // have one."
        builder.HasMany(r => r.Cities)
            .WithOne(c => c.Region)
            .HasForeignKey(c => c.RegionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
