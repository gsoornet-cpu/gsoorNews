using Jusoor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jusoor.Infrastructure.Persistence.Configurations;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Countries");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(100);
        builder.Property(c => c.NameEn).IsRequired().HasMaxLength(100);
        builder.Property(c => c.IsoCode2).IsRequired().HasMaxLength(2);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(100);

        builder.HasIndex(c => c.Slug).IsUnique();
        builder.HasIndex(c => c.IsoCode2).IsUnique();

        builder.HasMany(c => c.Cities)
            .WithOne(city => city.Country)
            .HasForeignKey(city => city.CountryId)
            .OnDelete(DeleteBehavior.Restrict); // never cascade-delete geography a country's news depends on
    }
}
